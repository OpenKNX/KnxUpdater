# KnxUpdater

This console app is for updating a Knx-Device over TwistedPair.  


## Requirements
You will have to use the [UpdateModule](https://github.com/thewhobox/KNX-IRModul/blob/master/src/UpdateModule.h).  
You can Copy+Paste it in your project.  
Then you need to add it to common:
```C++
#include <Arduino.h>
#include "OpenKNX.h"
#include "IrCodeModule.h"
#include "UpdateModule.h"

void setup()
{
	const uint8_t firmwareRevision = 0;
	openknx.init(firmwareRevision);
	openknx.addModule(1, new IrCodeModule());
	openknx.addModule(2, new UpdateModule());
	openknx.setup();
}

void loop()
{
	openknx.loop();
}
```

## Speed
On an empty Bus we can get up to 570 Bytes/s.  
|FileSize|Time|
|---|---|
|67 kB|2 min|
|100 kB| 3 min|
|150 kB| 4,5 min|
|200 kB| 6 min|
|250 kB| 7,5 min|
|300 kB| 9 min|


>You can speed up if you gzip yout bin file.

## Usage
You can use this console app on windows, linux or mac.  
The specified arguments are:  
>knxupdater help

Will show you the arguments and its definition.

>knxupdater \<IP-Address> \<PhysicalAddress> \<PathToFirmware> (\<Port> \<Delay>)

|Argument|Definition|
|---|---|
|IP-Address|IP of the KNX-IP-interface|
|PhysicalAddress|Address of the KNX-Device (ex. 1.2.120)|
|PathToFirmware|Path to the firmware.bin or firmware.bin.gz|
|Port|Optional - Port of the KNX-IP-interface (default 3671)|
|Delay|Optional - Delay after each telegram|