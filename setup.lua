--luanet.load_assembly("System.Speech.Recognition");

--Setup lorei's keywords--
RegisterLoreiName("System");
RegisterLoreiName("Lorei");
RegisterLoreiName("Viki");

--Setup Functions--
RegisterLoreiFunction("Launch");
RegisterLoreiFunction("Close");

--Setup Programs--
RegisterProgramWithScript("Winamp");
RegisterProgramWithScript("Command");
RegisterProgramWithScript("Ventrilo");
RegisterProgramWithScript("Pandora");