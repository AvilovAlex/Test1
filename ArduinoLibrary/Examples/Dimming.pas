uses ArduinoABC;

begin
  var delay := 2;
//  Arduino.UploadBoard('COM4');
  var myUno := new Arduino('COM4', 10, true);
  myUno.SetPinMode(11, AnalogOutput);
  while (true) do
  begin
    for var i := 0 to 255 do
    begin
      myUno.AnalogWrite(11, i);
      Sleep(delay);
    end;
    for var i := 255 downto 0 do
    begin
      myUno.AnalogWrite(11, i);
      Sleep(delay);
    end;
  end;
end.