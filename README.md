# Bluetooth Low Engergy scanner for Govee H5100 Temperature and Humidity Sensor

This repository shows how to build a scanner that will read the BLE advertisements from the Govee H5100 Temperature and Humidity Sensor. The scanner will extract the temperature and humidity data from the advertisement packets and display it in a human-readable format.

This application relied heavily on the repository [Govee BT Temp Logger](https://github.com/wcbonner/GoveeBTTempLogger) 

## BLE broadcast 

The Govee H5100 sensor broadcasts its data using BLE advertisements.  These are small packets broadcast to all listeners in range and do not require "pairing" or connection.  C# has a Windows native API that allow receiving advertisements.  This requires the **Windows.Devices.Bluetooth.Advertisement** namespace.  This application creates a **BluetoothLEAdvertisementWatcher** object that listens for advertisements performs all the low level work of receiving and parsing the packets.  

## Govee H5100 data embedded in the advertisement

The Govee H5100 sensor embeds temperature, humidity and battery data in the advertisement packet.  When a packet is received, the BLE advertisement watcher parses the packet into a **BluetoothLEAdvertisementReceivedEventArgs** object.  This object contains a **RawSignalStrengthInDBm** property that is the signal strength of the advertisement, a **BluetoothAddress** property that is the MAC address of the device, a **ManufacturerData** property that contains the manufacturer-specific data, and a **Advertisement** property that contains the advertisement data.  

*Although multiple sources claim that the data is embedded in the **ManufacturerData** property, this is not the case.  The data is actually embedded in the **Advertisement** property in the DataSections array.  The sensor data can be found in the fourth (index 3) data section of the array.*

## DataSection[3] format

This data section contains 8 bytes of data.  Using the library, the data must be read as a byte stream.  This application uses a **DataReader** object to read the data buffer as a stream.  The data is embedded as follows:

| Byte 0 | Byte 1 | Byte 2 | Byte 3 | Byte 4 | Byte 5 | Byte 6 | Byte 7 |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
|NA|NA|NA|NA|Temp & Humidty MSB|Temp & Humidity Byte|Temp & Humidity LSB|Battery| 


So how do you embed both temperature and humidity as floating point numbers in 3 bytes? The answer is in the range needed for the sensors.  The Govee H5100 temperature sensor has a range of -20 to 60 degrees Celsius and an accuracy of 0.1 degrees.  The humidity sensor has a range of 0 to 100% and an accuracy of 0.1%.  This means that the temperature can be represented as a signed integer in the range of -200 to 600 and the humidity can be represented as an unsigned integer in the range of 0 to 1000.  

First the temperature value is stored as tenths of degrees as an unsigned integer.  A temperature of 25.3 degrees Celsius would be stored as 253.  Then the value is multipled by 1000 to make room for the humidity value.  The humidity value in tenths of percent is then added to the temperature value.  For example, a humidity of 45.6% would be stored as 456.  The final value would be 253000 + 456 = 253456.  Finally since temperature can be negative, a sign bit (negative = 1) is added to the most significant bit of the 3 bytes.  

To extract the values the following steps are used.  First the most significant bit is checked to see if the temperature is negative.  Then that bit is cleared.  Humidity is extracted by taking the value modulo 1000.  Since it is still in tenths of a percent, it is divided by 10 to get the final humidity value and stored as a floating point number.  Then the original modulo value is subtracted from the original value.  The temperature can then be extracted by dividing the remaining value by 1000.  Since it is still in tenths of a degree, it is divided by an additional 10 to get the final temperature value and stored as a floating point number.  In the application, this division is accomplished in one step by dividing by 10,000 once.  If the sign bit was set, the temperature value is negated to get the final temperature value.  In the application, the temperature is converted to Fahrenheit.  

The battery value is stored as a single byte and is extracted by simply reading the byte value.  The battery value is stored as a percentage from 0 to 100 where 100 is fully charged and 0 is empty.

# Additional Functions

The Govee H5100 broadcasts use a fixed name.  The application uses a simple list lookup to convert these devices to a human-readable name.   This list is stored in a JSON file and is read at startup.  

# Future Work

Obviously this application was developed as a minimal proof of concept.  A production application would be more robust and rather than simply printing the data to the console, it would store the updates in some type of broker that could then be retrieved and displayed or processedby other applications.  The application could also be extended to support other devices that use BLE advertisements including those that encrypt the data.  




```

