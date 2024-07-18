using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Text;

namespace _0716
{
    /* 
     * 1.콘솔창 스크롤시 확대,축소로 세밀한 그래픽표현이 가능하므로
     * 콘솔 실행시 화면 창 크기설정 (창크기 수정, 크기 수정방지기능)
     * 
     * 2.캐릭터, 운석의 움직임 그래픽표현을 위해 매초단위로 콘솔창찍기
     * 
     * # 1,2 를 위해 while문으로 구현
     * 
     * 3.캐릭터 좌표정보와 이동기능 구현
     * 
     * 4.운석 좌표정보와 이동기능 구현
     * 
     * 5.캐릭터 운석 충돌판정
     * 
     * 6.생존 시간 타이머 구현
     * 
     * 7.결과내역 출력
     */

    internal class Program
    {
        #region CONST
        private const string TITLE_TEXT = "운석피하기";
        private const string START_TEXT = "게임시작";
        private const string ESCAPE_TEXT = "게임종료";
        private const string OPEN_MAIN_TEXT = "메인메뉴로 가기";
        private const string DEAD_MENT_TEXT = "운석을 피하지 못했습니다!";

        private const char BLOCK = '\u25A4'; // '▤'
        private const char EMPTY = '\u3000'; // '　'
        private const char CIRCLE = '\u25CF'; // '●'
        private const char METEO = '\u25C8'; // '◆'
        
        // 콘솔창에서 특수문자의 경우 일반문자 2칸을 차지하게됨
        private const int WIDTH = 40;
        private const int HEIGHT = 20;

        // 운석이 아래방향으로 이동되는 주기(s)
        private const float METEODROP_SPEED = 0.4f;

        // 바닥의 높이
        private static int LAND_HEIGHT = 3;
        #endregion

        // 초당 프레임
        private static int _fps = 60;

        // 화면에 출력해야할 특수문자배열
        private static char[,] _pixels = null;

        private static Player _player;
        private static ConsoleKey _input;

        private static Random _rand = new Random();
        private static StringBuilder _timePrinter = new StringBuilder();

        #region 시간관련 변수

        public static DateTime _startTime;
        private static float _currentTime = 0;
        private static int _timeRecord = 0;

        private static float _frameDealy = 0;
        private static float _frameRecord = 0;

        public static float _meteoSpawnDelay = 0f;
        private static float _meteoDropTimer = 0f;
        
        #endregion

        public static bool _isDead = false;
        public static int _levelSec = 0;

        // 캐싱을 위한 변수
        private static int _temp = 0;
        
        private static void Main(string[] args)
        {
            WindowController.SetConsoleSize(WIDTH, HEIGHT);
            Thread t1 = new Thread(WindowController.FixWindowSize);

            Lobby();
        }

        #region 로비,메인,결과 동작로직

        private static void Lobby()
        {
            Console.Clear();

            ConsoleInit();

            PrintTitle();

            int cursorPoint = 0;
            int menuTextLine = 9;

            string[] lobbyTexts = { START_TEXT, ESCAPE_TEXT };

            MenuPrint(menuTextLine, cursorPoint,lobbyTexts);

            while (true)
            {
                _input = Console.ReadKey().Key;

                if(_input == ConsoleKey.UpArrow || _input == ConsoleKey.DownArrow)
                {
                    cursorPoint = (cursorPoint + 1) % 2;

                    MenuPrint(menuTextLine, cursorPoint, lobbyTexts);
                }

                if(_input == ConsoleKey.Enter)
                {
                    if (cursorPoint == 0)
                        break;

                    Environment.Exit(0);
                }
            }

            MainGame();
        }

        private static void MainGame()
        {
            GameInit();

            _meteoSpawnDelay = _rand.Next(1000, _levelSec) * 0.001f;

            while (true)
            {
                if (_isDead)
                {
                    break;
                }

                Update();
                Render();
            }

            Console.SetCursorPosition(0, 0);

            for (int i = 0; i < WIDTH; i++)
            {
                Console.Write(EMPTY);
            }

            PrintResultWindow();
        }

        /// <summary>
        /// 결과화면을 출력하며 이후 진행을 선택합니다.
        /// </summary>
        private static void PrintResultWindow()
        {
            int cursorPoint = 0;

            string[] menuText = { OPEN_MAIN_TEXT, ESCAPE_TEXT };

            int menuPrintLine = (HEIGHT / 2) - menuText.Length - 3;

            Console.SetCursorPosition(WIDTH / 2 - DEAD_MENT_TEXT.Length, menuPrintLine);
            Console.WriteLine(DEAD_MENT_TEXT);

            string record = $"생존시간 : {_timeRecord}초";

            Console.WriteLine(record.PadLeft(WIDTH / 2));
            Console.WriteLine();

            menuPrintLine += 3;

            MenuPrint(menuPrintLine, cursorPoint, menuText);

            while (true)
            {
                _input = Console.ReadKey().Key;

                if (_input == ConsoleKey.UpArrow || _input == ConsoleKey.DownArrow)
                {
                    cursorPoint = (cursorPoint + 1) % menuText.Length;

                    MenuPrint(menuPrintLine, cursorPoint, menuText);
                }

                if (_input == ConsoleKey.Enter)
                {
                    if (cursorPoint == 1)
                    {
                        Console.Clear();
                        Environment.Exit(0);
                    }

                    break;
                }
            }

            Lobby();
        }

        #endregion

        #region 초기화 함수

        /// <summary>
        /// 콘솔 실행 시 한번만 설정되는 옵션들 입니다.
        /// </summary>
        private static void ConsoleInit()
        {
            Console.OutputEncoding = Encoding.UTF8;

            Console.Title = "장동진_운석피하기";
            Console.CursorVisible = false;

            SetFrameDelay(_fps);
        }

        /// <summary>
        /// 게임시작에 앞서 각 변수들의 초기값을 설정합니다.
        /// </summary>
        private static void GameInit()
        {
            _startTime = DateTime.Now;
            GetTime();
            _timeRecord = 0;

            // 설정된 _width 변수의 절반을 설정한 이유는
            // 픽셀배열을 전부 특수문자로 채워 정사각형의 형태로 출력하기 위함입니다.
            _pixels = new char[HEIGHT , WIDTH/2];

            // 플레이어 시작위치는 바닥의 가운데로 설정합니다.
            _player = new Player() 
            { _posX = _pixels.GetLength(1)/2, _posY =HEIGHT-LAND_HEIGHT-1};
            
            // 픽셀 배열에 바닥, 빈공간, 플레이어 각 특수문자를 삽입합니다.
            PaintLand();
            PaintEmpty();
            _pixels[_player._posY, _player._posX] = CIRCLE;

            // 출력용 타이머 초기설정
            _timePrinter.Clear();
            _timePrinter.Append(0);
            Console.Write($"플레이타임 : {_timePrinter} 초");

            // 시작 난이도 설정
            _levelSec = 7500;
            _meteoDropTimer = METEODROP_SPEED;

            _isDead = false;
            _frameRecord = 0;
        }

        #endregion

        #region MainGame while문 생명주기

        private static void Render()
        {
            if (!SendRenderSignal())
                return;

            Console.SetCursorPosition(0, 0);

            Console.Write($"플레이타임 : {_timePrinter} 초");

            // 픽셀배열 출력
            RenderPexels();
        }

        private static void Update()
        {
            GetTime();

            WindowController.FixWindowSize();

            Input();

            UpdatePrintTime();

            MeteoSpawnCheck();

            MeteoDropCheck();
        }

        #endregion

        /// <summary>
        /// 메인 타이틀 출력함수
        /// </summary>
        private static void PrintTitle()
        {
            Console.SetCursorPosition(0, 2);

            for (int i = 0; i < WIDTH; i++)
            {
                Console.Write('#');
            }

            Console.SetCursorPosition(0, 3);
            Console.Write("##");

            Console.SetCursorPosition(WIDTH - 2, 3);
            Console.WriteLine("##");

            Console.SetCursorPosition(WIDTH / 2 - TITLE_TEXT.Length, 3);
            Console.WriteLine(TITLE_TEXT);

            for (int i = 0; i < WIDTH; i++)
            {
                Console.Write('#');
            }
        }

        /// <summary>
        /// 전달받은 문자열 요소들을 선택형 텍스트 UI 처럼 출력합니다.
        /// </summary>
        /// <param name="m_printLine">몇번째 줄부터 출력할지 결정</param>
        /// <param name="m_selectNum">문자열 배열요소중 N번째 요소를 선택합니다.</param>
        /// <param name="m_strs">메뉴료 표현할 텍스트 문자열</param>
        private static void MenuPrint(int m_printLine, int m_selectNum, string[] m_strs)
        {
            Console.SetCursorPosition(0, m_printLine);

            for (int i = 0; i < m_strs.Length; i++)
            {
                Console.SetCursorPosition(WIDTH / 2 - m_strs[i].Length, m_printLine + i);

                if (m_selectNum == i)
                {
                    Console.BackgroundColor = ConsoleColor.Gray;
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.WriteLine(m_strs[i]);
                    Console.ResetColor();
                }
                else
                    Console.WriteLine(m_strs[i]);
            }
        }

        /// <summary>
        /// 픽셀배열에 운석이 있다면 아래방향으로 이동시키는 함수
        /// </summary>
        private static void DropMeteo()
        {
            for (int i = HEIGHT - LAND_HEIGHT - 1; i > 0; i--)
            {
                for (int j = 0; j < _pixels.GetLength(1); j++)
                {
                    if (_pixels[i, j] == METEO)
                    {
                        if (_pixels[i + 1, j] == CIRCLE)
                        {
                            _isDead = true;
                            return;
                        }

                        if (_pixels[i + 1, j] == BLOCK)
                        {
                            _pixels[i, j] = EMPTY;
                            continue;
                        }

                        _pixels[i + 1, j] = METEO;
                        _pixels[i, j] = EMPTY;

                    }
                }
            }
        }

        /// <summary>
        /// 출력용 시간타이머를 관리합니다.
        /// </summary>
        private static void UpdatePrintTime()
        {
            if ((int)_currentTime <= _timeRecord)
                return;

            _timePrinter.Clear();
            _timeRecord = (int)_currentTime;
            _timePrinter.Append(_timeRecord);

            SetLevel();
        }

        /// <summary>
        /// 등록된 시간에 운성 생성주기를 감소시킵니다.
        /// </summary>
        private static void SetLevel()
        {
            switch (_timeRecord)
            {
                case 10:
                    _levelSec -= 2000;
                    break;

                case 30:
                    _levelSec -= 2000;
                    break;

                case 60:
                    _levelSec -= 2000;
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// 생성된 운석을 일정시간마다 아래로 내리기 위한 함수
        /// </summary>
        private static void MeteoDropCheck()
        {
            if (_meteoDropTimer >= _currentTime)
                return;

            DropMeteo();
            _meteoDropTimer += METEODROP_SPEED;
        }

        /// <summary>
        /// 일정시간마다 운석을 생성하기 위한 함수
        /// </summary>
        private static void MeteoSpawnCheck()
        {
            if (_meteoSpawnDelay - _currentTime >= 0)
                return;

            SpawnMeteo();
            _meteoSpawnDelay = (_rand.Next(1000, _levelSec) * 0.001f) + _currentTime;
        }

        /// <summary>
        /// 입력처리 로직
        /// 입력을 하지 않더라도 프레임은 계속 동작해야하므로
        /// 별도의 키 입력이 없을시 해당 함수를 종료시킵니다.
        /// </summary>
        private static void Input()
        {
            if (!Console.KeyAvailable)
                return;

            _input = Console.ReadKey(false).Key;

            switch (_input)
            {
                // 오른쪽 키 입력이면서
                case ConsoleKey.RightArrow:

                    // 배열영역 내부 범위라면 (출력화면영역 내부 범위)
                    if (_player._posX < _pixels.GetLength(1) - 1)
                    {
                        // 빈공간과 플레이어를 변경합니다.
                        _pixels[_player._posY, _player._posX] = EMPTY;
                        _player._posX += 1;
                        _pixels[_player._posY, _player._posX] = CIRCLE;
                    }

                    break;

                case ConsoleKey.LeftArrow:

                    if (0 < _player._posX)
                    {
                        _pixels[_player._posY, _player._posX] = EMPTY;
                        _player._posX -= 1;
                        _pixels[_player._posY, _player._posX] = CIRCLE;
                    }
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// 바닥 영역 데이터 입력함수
        /// 바닥 특수문자는 가장 하단부에 위치하므로 픽셀배열의 역순으로 입력시킵니다.
        /// </summary>
        private static void PaintLand()
        {
            for (int i = HEIGHT-LAND_HEIGHT; i < _pixels.GetLength(0); i++)
            {
                for (int j = 0; j < _pixels.GetLength(1); j++)
                {
                    _pixels[i, j] = BLOCK;
                }
            }
        }

        /// <summary>
        /// 픽셀 배열에 바닥영역을 제외한 모든영역을 빈공간으로 설정합니다.
        /// ' '
        /// '　' 일반 공백과 특수문자 공백은 공간차이가 있으므로 특수문자를 활용하였습니다.
        /// </summary>
        private static void PaintEmpty()
        {
            for (int i = 0; i < HEIGHT - LAND_HEIGHT; i++)
            {
                for (int j = 0; j < _pixels.GetLength(1); j++)
                {
                    _pixels[i, j] = EMPTY;
                }
            }
        }

        /// <summary>
        /// 현재 픽셀배열에 저장된 특수문자들을 전부 출력합니다.
        /// </summary>
        private static void RenderPexels()
        {
            for (int i = 0; i < _pixels.GetLength(0); i++) 
            {
                for (int j = 0; j < _pixels.GetLength(1); j++)
                {
                    Console.Write(_pixels[i, j]);
                }
                Console.WriteLine();
            }
        }

        /// <summary>
        /// 최상단라인의 무작위 위치에 운석을 스폰하며, 픽셀데이터에 운석 특수문자를 삽입합니다.
        /// </summary>
        private static void SpawnMeteo()
        {
            _temp = _rand.Next(0, _pixels.GetLength(1));

            _pixels[1, _temp] = METEO;
        }
        // 1프레임시 1초당 1프레임 출력 1000ms/1fps 딜레이 1000ms
        // 60프레임시 1초당 1000ms/60fps = 16.66666...

        /// <summary>
        /// 초당 출력될 프레임값을 전달받습니다.
        /// 1초 내의 모든 프레임간의 간격 interval을 측정합니다.
        /// </summary>
        public static void SetFrameDelay(int m_fps)
        {
            _frameDealy = 1000 / m_fps * 0.001f;
        }

        /// <summary>
        /// 설정된 프레임 딜레이간격을 측정하여
        /// 딜레이주기가 초과될 시 true값을 반환합니다.
        /// </summary>
        public static bool SendRenderSignal()
        {
            if (_currentTime - _frameRecord >= _frameDealy)
            {
                _frameRecord = _currentTime;

                return true;
            }

            return false;
        }

        /// <summary>
        /// 게임 내 진행되는 시간을 측정합니다.
        /// </summary>
        public static void GetTime() // 1f = 1sec , 0.1f = 0.1sec
        {
            _currentTime = (float)(DateTime.Now - _startTime).TotalSeconds;
        }
    }

    public struct Player
    {
        public int _posX;
        public int _posY;
    }

    public static class WindowController
    {
        private static int _width = 0;
        private static int _height = 0;

        /// <summary>
        /// 콘솔 화면에 출력될 텍스트 갯수를 설정합니다.
        /// </summary>
        public static void SetConsoleSize(int m_width, int m_height)
        {
            _width = m_width;
            _height = m_width;

            FixWindowSize();
        }

        /// <summary>
        /// 콘솔창의 크기 변경을 방지합니다.
        /// </summary>
        public static void FixWindowSize()
        {
            if (Console.WindowWidth != _width || Console.WindowHeight != _height)
                Console.SetWindowSize(_width, _height);
        }
    }
}
