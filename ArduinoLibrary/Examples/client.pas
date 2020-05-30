{$reference ArduinoToPascal.dll}
uses ArduinoToPascal;
procedure MyUno_PinChanged(sender: Arduino; pin: integer; value: integer);
begin
  Console.WriteLine('Вход {0} поменял свое значение на {1}', pin, value);
end;

begin
  var myUno := new Arduino();
  Console.WriteLine('We find Arduino on {0}, with {1} baund rate per secon',
  myUno.connectInfo.comPort, myUno.connectInfo.baudRate);
  myUno.PinChanged += MyUno_PinChanged;
  
  while (true) do
  begin
    Console.WriteLine('"Moment value of 4 pin = {0}', myUno.AnalogRead(4));
    myUno.DigitalWrite(13, true);
    sleep(500);
    myUno.DigitalWrite(13, false);
    sleep(500);
  end;
end.