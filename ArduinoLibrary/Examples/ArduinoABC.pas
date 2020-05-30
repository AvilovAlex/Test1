unit ArduinoABC;
{$reference ..\ResultDll\ArduinoLibrary.dll}

type
  Arduino = ArduinoLibrary.Arduino;

const
  AnalogInput = Arduino.PinMode.AnalogInput;
  AnalogOutput = Arduino.PinMode.AnalogOutput;
  DigitalInput = Arduino.PinMode.DigitalInput;
  DigitalOutput = Arduino.PinMode.DigitalOutput;
end.