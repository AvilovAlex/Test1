{$reference ArduinoToPascal.dll}
uses ArduinoToPascal;

var
  myUno := new Arduino('COM4');
  myUno2 := new Arduino('COM5');
  enable := false;

procedure MyUno_DigitalPinChanged(sender: Arduino; pin: integer; value: boolean);
begin
  if ((pin = 4) and (value)) then
  begin
    myUno2.DigitalWrite(11, enable);
    enable := not enable;
  end;
end;

begin
  myUno.DigitalPinChanged += MyUno_DigitalPinChanged;
  myUno.SetPinMode(4, Arduino.PinMode.DigitalInput);
  myUno2.SetPinMode(11, Arduino.PinMode.DigitalOutput);
  
  while (true) do
end.