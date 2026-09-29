#include <Arduino.h>
#include <BLEDevice.h>
#include <BLEUtils.h>
#include <BLEScan.h>
#include <BLEAdvertisedDevice.h>

int scanTime = 5; // Scan duration in seconds
BLEScan* pBLEScan;

// Callback class to handle discovered devices
class MyAdvertisedDeviceCallbacks: public BLEAdvertisedDeviceCallbacks {
    void onResult(BLEAdvertisedDevice advertisedDevice) 
    {
      std::string deviceName = advertisedDevice.getName();
      const size_t BUF_SIZE = 32;
      char deviceData[BUF_SIZE];

      char *sourceBuffer = (char*)advertisedDevice.getManufacturerData().c_str();  
      int copySize = advertisedDevice.getManufacturerData().length() < BUF_SIZE ? advertisedDevice.getManufacturerData().length() : BUF_SIZE; 
      memcpy(deviceData, sourceBuffer, copySize); 

      if (deviceName.length() > 0)
      {
        if (deviceName.find("5100") != std::string::npos) 
        {
          if (deviceData != nullptr) 
          { 
            int batteryLevel = (int)deviceData[7]; 
            uint tempAndHumidityBuffer = ((uint)deviceData[4] << 16) | ((uint)deviceData[5] << 8) | deviceData[6];
            bool tempIsNegative = (tempAndHumidityBuffer & 0x800000) != 0;
            tempAndHumidityBuffer &= 0x7FFFFF;
            uint iHumidity = tempAndHumidityBuffer % 1000;
            float fTempC = (tempAndHumidityBuffer- iHumidity) / 10000.0f;
            if (tempIsNegative)
            {
                fTempC = -fTempC;  // if the negative bit is set, make the temperature negative
            }

            float tempF = (fTempC * 9.0f / 5.0f) + 32.0f; // convert to Fahrenheit
            float humidity = iHumidity / 10.0f; 
            Serial.printf("Device Name: %s, Temp: %.1f°F Humidity: %.1f%%, Battery Level: %d%%\n", deviceName.c_str(), tempF, humidity, batteryLevel);
          } 
        }
      }
    }    
};


void setup() {
  Serial.begin(115200);
  //Serial.println("Scanning...");

  // Initialize the BLE device
  BLEDevice::init("");
  
  // Create the scan object
  pBLEScan = BLEDevice::getScan(); 
  pBLEScan->setAdvertisedDeviceCallbacks(new MyAdvertisedDeviceCallbacks());
  pBLEScan->setActiveScan(false); // Active scan uses more power, but gets more data
  pBLEScan->setInterval(100);
  pBLEScan->setWindow(99);  // Less than or equal to setInterval
}

void loop() {
  // Start the scan and wait for it to finish before continuing
  BLEScanResults foundDevices = pBLEScan->start(scanTime, false);
  //Serial.print("Devices found: ");
  //Serial.println(foundDevices.getCount());
  //Serial.println("Scan done!");
  
  pBLEScan->clearResults();   // Delete results from BLEScan buffer to release memory
  delay(20000);                // Wait 2 seconds before scanning again
}

