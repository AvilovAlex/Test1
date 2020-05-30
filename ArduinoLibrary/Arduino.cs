using Solid.Arduino;
using System;
using System.Collections.Generic;
using Firmata;
using System.Linq;
using ArduinoUploader;
using System.Threading;

namespace ArduinoLibrary
{
    /// <summary>
    /// Класс, позволяющий взаимодействовать с платформой Arduino
    /// </summary>
    public class Arduino
    {
        /// <summary>
        /// Тип свойств соединения
        /// </summary>
        public struct ConnectInfoStruct
        {
            /// <summary>
            /// Номер COM-порта в формате COM^\d
            /// </summary>
            public string comPort;

            /// <summary>
            /// Скорость обмена данных
            /// </summary>
            public int baudRate;
        }

        /// <summary>
        /// Перечислимый тип режимов работы входов/выходов
        /// </summary>
        public enum PinMode
        {
            /// <summary>
            /// Выход отключен
            /// </summary>
            Off,
            /// <summary>
            /// Дискретный вход
            /// </summary>
            DigitalInput,
            /// <summary>
            /// Дискретный выход
            /// </summary>
            DigitalOutput,
            /// <summary>
            /// Аналоговый вход
            /// </summary>
            AnalogInput,
            /// <summary>
            /// Аналоговый выход
            /// </summary>
            AnalogOutput
        }

        /// <summary>
        /// Тип состояния аналогового входа
        /// </summary>
        private struct AnalogPinStateStruct
        {
            /// <summary>
            /// Состояние входа
            /// </summary>
            public int pinValue;
            /// <summary>
            /// Режим входа/выхода
            /// </summary>
            public PinMode pinMode;
            public AnalogPinStateStruct(int pinV, PinMode pinM)
            {
                pinValue = pinV;
                pinMode = pinM;
            }
        }
        /// <summary>
        /// Тип состояния дискретного входа
        /// </summary>
        private struct DigitalPinStateStruct
        {
            /// <summary>
            /// Состояние входа
            /// </summary>
            public bool pinValue;
            /// <summary>
            /// Режим входа/выхода
            /// </summary>
            public PinMode pinMode;
            public DigitalPinStateStruct(bool pinV, PinMode pinM)
            {
                pinValue = pinV;
                pinMode = pinM;
            }
        }

        /// <summary>
        /// Класс, обеспечивающий связь с микроконтроллером Arduino
        /// </summary>
        private FirmataVB driver;

        /// <summary>
        /// Фильтрующая погрешность, на изменение которой не будут приходить события
        /// </summary>
        private int threshold;

        /// <summary>
        /// Фильтрующая погрешность, на изменение которой не будут приходить события
        /// </summary>
        public int Delta { get => threshold; set => threshold = (value > 0) && (value < 100) ? threshold : 10; }

        /// <summary>
        /// Делегат события изменения состояния аналогого входа
        /// </summary>
        /// <param name="sender">Экземпляр микрокотроллера, от которого пришло событие</param>
        /// <param name="pin">Номер входа, изменившего своего состояния</param>
        /// <param name="value">Новое состояние, пришеднее от входа</param>
        public delegate void AnalogPinChangeHandler(Arduino sender, int pin, int value);

        /// <summary>
        /// Событие изменения состояния аналогого входа
        /// </summary>
        public event AnalogPinChangeHandler AnalogPinChanged;

        /// <summary>
        /// Делегат события изменения состояния входа
        /// </summary>
        /// <param name="sender">Экземпляр микрокотроллера, от которого пришло событие</param>
        /// <param name="pin">Номер входа, изменившего своего состояния</param>
        /// <param name="value">Новое состояние, пришеднее от входа</param>
        public delegate void DigitalPinChangeHandler(Arduino sender, int pin, bool value);

        /// <summary>
        /// Событие изменения состояния входа
        /// </summary>
        public event DigitalPinChangeHandler DigitalPinChanged;

        /// <summary>
        /// Словарь, хранящий в себе состояния аналоговых входов микроконтроллера и обновляющийся по событиям от платы
        /// </summary>
        private Dictionary<int, AnalogPinStateStruct> analogPinState;

        /// <summary>
        /// Словарь, хранящий в себе состояния дискретных входов микроконтроллера и обновляющийся по событиям от платы
        /// </summary>
        private Dictionary<int, DigitalPinStateStruct> digitalPinState;

        /// <summary>
        /// Словарь, хранящий в себе состояния PWM выходов микроконтроллера и обновляющийся по событиям от платы
        /// </summary>
        private Dictionary<int, AnalogPinStateStruct> pwmPinState;

        /// <summary>
        /// Словарь байт-состояний двух портов
        /// </summary>
        private Dictionary<int, int> portsState;

        /// <summary>
        /// Свойства соединения
        /// </summary>
        public ConnectInfoStruct connectInfo;

        /// <summary>
        /// Режим логирования
        /// </summary>
        public bool LogMode;

        /// <summary>
        /// Метод автоматического поиска платы Arduino, Подключенной к ПК
        /// </summary>
        /// <returns></returns>
        private ConnectInfoStruct GetConnectionInfo(string comPort)
        {
            ISerialConnection currentConnection = null;
            ConnectInfoStruct connectInfo = new ConnectInfoStruct();
            if (comPort == "auto")
            {
                if (LogMode) Console.WriteLine("Выполняется поиск платы Arduino");
                try
                {
                    currentConnection = EnhancedSerialConnection.Find();
                }
                catch
                {
                    if (LogMode) Console.WriteLine("Плата Arduino найдена, проблема с подключением. " +
                        "убедитесь, что Ваша плата Arduino подключена корректно.");
                }

                if (currentConnection == null)
                {
                    if (LogMode) Console.WriteLine("Плата Arduino не найдена, убедитесь, что Ваша плата Arduino подключена по USB.");
                    return connectInfo;
                }

                if (LogMode) Console.WriteLine("Плата Arduino найдена на {0} со скоростью {1}",
                    currentConnection.PortName,
                    currentConnection.BaudRate);
                connectInfo.comPort = currentConnection.PortName;
                connectInfo.baudRate = currentConnection.BaudRate;

                ArduinoSession x = new ArduinoSession(currentConnection);
                x.SendStringData(FirmataVB.SYSTEM_RESET.ToString());
                x.ResetBoard();
                x.Clear();
                x.Dispose();
                currentConnection.Close();
            }
            else
            {
                connectInfo.comPort = comPort;
                connectInfo.baudRate = 57600;
            }
            if (LogMode) Console.WriteLine("Соединение установлено.");
            return connectInfo;
        }

        /// <summary>
        /// Процедура, инициализирующая словарь состояний входов
        /// </summary>
        private void InitPinStates()
        {
            analogPinState = new Dictionary<int, AnalogPinStateStruct>();
            digitalPinState = new Dictionary<int, DigitalPinStateStruct>();
            portsState = new Dictionary<int, int>();
            pwmPinState = new Dictionary<int, AnalogPinStateStruct>();
            for (int i = 0; i < 6; i++)
                analogPinState.Add(i, new AnalogPinStateStruct());
            for (int i = 0; i < 14; i++)
                digitalPinState.Add(i, new DigitalPinStateStruct());
            for (int i = 0; i < 2; i++)
                portsState.Add(i, 0);
            int[] pwnMap = new int[] { 3, 5, 6, 9, 10, 11 };
            for (int i = 0; i < pwnMap.Count(); i++)
                pwmPinState.Add(pwnMap[i], new AnalogPinStateStruct());
        }

        /// <summary>
        /// Процедура загрузки скетча в микроконтроллер Arduino
        /// </summary>
        /// <param name="comPort"></param>
        /// <param name="sketchPath"></param>
        static public void UploadBoard(string comPort, string sketchPath = "auto")
        {
            Console.WriteLine("Начата загрузка скетча");
            if (sketchPath == "auto")
                sketchPath = @"..\ArduinoSketch\StandardFirmata.ino.standard.hex";
            var uploader = new ArduinoSketchUploader(
            new ArduinoSketchUploaderOptions()
            {
                FileName = sketchPath,
                PortName = comPort,
                ArduinoModel = ArduinoUploader.Hardware.ArduinoModel.UnoR3
            });

            uploader.UploadSketch();
            Thread.Sleep(1000);
            Console.WriteLine("Cкетч загружен");
        }

        /// <summary>
        /// Конструктор класса с режимом автоматического поиска 
        /// </summary>
        /// <param name="comPort">COM-порт в формате COM\d</param>
        /// <param name="delta">Погрешность измерения аналогого сигнала</param>
        /// <param name="logMode">Вкл/Отк режима логировния аварийных сообщений</param>
        public Arduino(string comPort = "auto", int delta = 10, bool logMode = false)
        {
            LogMode = logMode;
            Delta = delta;

            connectInfo = GetConnectionInfo(comPort);
            driver = new FirmataVB
            {
                COMPortName = connectInfo.comPort,
                Baud = connectInfo.baudRate
            };
            driver.Connect();

            InitPinStates();

            SetAnalogReportMode(true);
            SetDigitalReportMode(true);
            driver.AnalogMessageReceieved += Driver_AnalogMessageReceieved;
            driver.DigitalMessageReceieved += Driver_DigitalMessageReceieved;
        }

        /// <summary>
        /// Процедура активирующая отправку событий об изменении аналоговых входов
        /// </summary>
        /// <param name="enable">Включено/Выключено</param>
        private void SetAnalogReportMode(bool enable)
        {
            for (int pin = 0; pin < 6; pin++)
                driver.AnalogPinReport(pin, enable ? 1 : 0);
        }

        /// <summary>
        /// Процедура активирующая отправку событий об изменении аналоговых входов
        /// </summary>
        /// <param name="enable">Включено/Выключено</param>
        private void SetDigitalReportMode(bool enable)
        {
            for (int port = 0; port < 2; port++)
                driver.DigitalPortReport(port, enable ? 1 : 0);
        }

        /// <summary>
        /// Обработчик сообщений Firmata об изменении состояния аналоговых входов
        /// </summary>
        /// <param name="pin">Номер входа, изменившего своего состояния</param>
        /// <param name="value">Новое состояние, пришеднее от входа</param>
        private void Driver_AnalogMessageReceieved(int pin, int value)
        {
            //Так как Firmata пыосылает события не только на фактические изменения состояния входов
            //Необходимо отфильтровать помехи сообщений
            if (Math.Abs(analogPinState[pin].pinValue - value) > Delta)
            {
                analogPinState[pin] = new AnalogPinStateStruct(value, analogPinState[pin].pinMode);
                if (AnalogPinChanged != null)
                {
                    EventArgs eventArgs = new EventArgs();
                    AnalogPinChanged(this, pin, value);
                }
            }
        }

        /// <summary>
        /// Обработчик сообщений Firmata об изменении состояния дискретных входов
        /// </summary>
        /// <param name="portNumber">Номер порта, изменившего своего состояния</param>
        /// <param name="portData">Новое состояние, пришеднее от порта</param>
        private void Driver_DigitalMessageReceieved(int portNumber, int portData)
        {
            if (portNumber > 1) return;
            //Так как Firmata пыосылает события не только на фактические изменения состояния входов
            //Необходимо отфильтровать помехи сообщений

            //Проверим, что байт-значение отличается
            //Дополнительная проверка нужна, что-бы не запускать цикл попусту
            if (portsState[portNumber] != portData)
            {
                portsState[portNumber] = portData;
                // Проверим состояние каждого пина так как мы не знаем, на изменение какого конкретно подписан пользователь
                for (int pin = 0; pin < 8; pin++)
                {
                    //Достанем состояние конкретного пина из значение байта путем сдвига на количество пинов
                    bool pinData = ((portData >> pin) & 1) == 1;
                    if (digitalPinState[pin + portNumber * 8].pinValue != pinData)
                    {
                        //Обновим значение коллекции и в случае подписи на делегат, вызовем событие изменения
                        digitalPinState[pin + portNumber * 8] =
                            new DigitalPinStateStruct(pinData,
                            digitalPinState[pin + portNumber * 8].pinMode);
                        if (DigitalPinChanged != null)
                        {
                            EventArgs eventArgs = new EventArgs();
                            DigitalPinChanged(this, pin + portNumber * 8, pinData);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Предикат аналогого режима записи или чтения
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        private bool AnalogModePred(PinMode mode)
        {
            return (mode == PinMode.AnalogInput || mode == PinMode.AnalogOutput);
        }

        /// <summary>
        /// Предикат, проверяющий, поддерживает ли выход PWM
        /// </summary>
        /// <param name="pin">Номер выхода</param>
        /// <returns></returns>
        private bool PwmPred(int pin)
        {
            return (pin == 3) ||
                (pin == 5) ||
                (pin == 6) ||
                (pin == 9) ||
                (pin == 10) ||
                (pin == 11);
        }

        /// <summary>
        /// Процедура задания режима входа/выхода
        /// </summary>
        /// <param name="pinNumber">Номер входа/выхода (1..13)</param>
        /// <param name="pinMode">Режим входа/выхода</param>
        public void SetPinMode(int pinNumber, PinMode pinMode)
        {
            switch (pinMode)
            {
                case PinMode.Off:
                    return;
                case PinMode.DigitalInput:
                    if ((pinNumber < 2) || (pinNumber > 13))
                    {
                        if (LogMode) Console.WriteLine("Ошибка, дискретный вход, для которого вы задаете режим, не существует");
                        return;
                    }
                    digitalPinState[pinNumber] = new DigitalPinStateStruct(false, pinMode);
                    driver.PinMode(pinNumber, FirmataVB.INPUT);
                    break;
                case PinMode.DigitalOutput:
                    if ((pinNumber < 2) || (pinNumber > 13))
                    {
                        if (LogMode) Console.WriteLine("Ошибка, дискретный выход, для которого вы задаете режим, не существует");
                        return;
                    }
                    digitalPinState[pinNumber] = new DigitalPinStateStruct(false, pinMode);
                    break;
                case PinMode.AnalogInput:
                    if ((pinNumber < 0) || (pinNumber > 5))
                    {
                        if (LogMode) Console.WriteLine("Ошибка, аналоговый вход, для которого вы задаете режим, не существует");
                        return;
                    }
                    analogPinState[pinNumber] = new AnalogPinStateStruct(0, pinMode);
                    break;
                case PinMode.AnalogOutput:
                    if (!PwmPred(pinNumber))
                    {
                        if (LogMode) Console.WriteLine("Ошибка, аналоговый выход, для которого вы задаете режим, не существует");
                        return;
                    }
                    pwmPinState[pinNumber] = new AnalogPinStateStruct(0, pinMode);
                    driver.PinMode(pinNumber, FirmataVB.PWM);
                    break;
                default:
                    if (LogMode) Console.WriteLine("Ошибка, неверный режим");
                    break;
            }
        }

        /// <summary>
        /// Процедура перевода режима из строкового представления в перечислимое
        /// </summary>
        /// <param name="pinMode">Режим входа/выхода {DigitalInput, DigitalOutput, AnalogInput, AnalogOutput}</param>
        /// <returns></returns>
        public PinMode SrtPinMode(string pinMode)
        {
            switch (pinMode)
            {
                case "AnalogInput":
                    return PinMode.AnalogInput;
                case "DigitalInput":
                    return PinMode.DigitalInput;
                case "DigitalOutput":
                    return PinMode.DigitalOutput;
                case "AnalogOutput":
                    return PinMode.AnalogOutput;
                default:
                    return PinMode.Off;
            }
        }

        /// <summary>
        /// Процедура задания режима входа/выхода
        /// </summary>
        /// <param name="pinNumber">Номер входа/выхода (1..13)</param>
        /// <param name="pinMode">Режим входа/выхода {DigitalInput, DigitalOutput, AnalogInput, AnalogOutput}</param>
        public void SetPinMode(int pinNumber, string pinMode)
        {
            SetPinMode(pinNumber, SrtPinMode(pinMode));
        }

        //Основные методы

        /// <summary>
        /// Возвращает булево значение {false, true} дискретного входа, false в случае чтения с выхода
        /// </summary>
        /// <param name="pinNumber">Номер входа</param>
        /// <returns></returns>
        public bool DigitalRead(int pinNumber)
        {
            //Проверка номера входа (Для считывания дискретного сигнала доступны только входы 0..5
            if ((pinNumber < 2) || (pinNumber > 13))
            {
                if (LogMode) Console.WriteLine("Ошибка, Вы пытаетесь считать дискретное значение из несуществующего дискретного входа");
                return false;
            }
            //Проверка режима выхода
            if (digitalPinState[pinNumber].pinMode != PinMode.DigitalInput)
            {
                if (LogMode) Console.WriteLine("Ошибка чтения, команда не соответствует режиму входа");
                return false;
            }
            return digitalPinState[pinNumber].pinValue;
        }

        /// <summary>
        /// Процедура записи дискретного значения
        /// </summary>
        /// <param name="pinNumber">Номер выхода</param>
        /// <param name="value">значение</param>
        public void DigitalWrite(int pinNumber, bool value)
        {
            //Проверка номера выхода (0 и 1 выходы зарезервированны под связь с ПК)
            if ((pinNumber < 2) || (pinNumber > 13))
            {
                if (LogMode) Console.WriteLine("Ошибка, Вы пытаетесь установить дискретное значение в несуществующий дискретный выход");
                return;
            }
            //Проверка режима выхода
            if (digitalPinState[pinNumber].pinMode != PinMode.DigitalOutput)
            {
                if (LogMode) Console.WriteLine("Ошибка записи, команда не соответствует режиму выхода");
                return;
            }

            driver.DigitalWrite(pinNumber, value ? 1 : 0);
        }

        /// <summary>
        /// Возвращает целочисленное значение (0..100) аналогового входа, -1 в случае чтения с выхода
        /// </summary>
        /// <param name="pinNumber">Номер входа</param>
        /// <returns></returns>
        public int AnalogRead(int pinNumber)
        {
            //Проверка номера входа (Для считывания дискретного сигнала доступны только входы 0..5
            if ((pinNumber < 0) || (pinNumber > 5))
            {
                if (LogMode) Console.WriteLine("Ошибка, Вы пытаетесь считать аналоговое значение из несуществующего аналогого входа");
                return -1;
            }
            //Проверка режима выхода
            if (analogPinState[pinNumber].pinMode != PinMode.AnalogInput)
            {
                if (LogMode) Console.WriteLine("Ошибка чтения, команда не соответствует режиму входа");
                return -1;
            }
            return analogPinState[pinNumber].pinValue;
        }

        /// <summary>
        /// Процедура записи аналогого значения
        /// </summary>
        /// <param name="pinNumber">Номер выхода</param>
        /// <param name="value">значение</param>
        public void AnalogWrite(int pinNumber, int value)
        {
            //Проверка номера выхода (0 и 1 выходы зарезервированны под связь с ПК)
            if (!PwmPred(pinNumber))
            {
                if (LogMode) Console.WriteLine("Ошибка, Вы пытаетесь установить аналоговое значение в несуществующий аналоговый выход");
                return;
            }
            //Проверка режима выхода
            if (pwmPinState[pinNumber].pinMode != PinMode.AnalogOutput)
            {
                if (LogMode) Console.WriteLine("Ошибка записи, команда не соответствует режиму выхода");
                return;
            }

            driver.AnalogWrite(pinNumber, value);
        }

        /// <summary>
        /// Деструктор класа Arduino
        /// </summary>
        ~Arduino()
        {
            driver.Disconnect();
            driver.Dispose();
        }
    }
}