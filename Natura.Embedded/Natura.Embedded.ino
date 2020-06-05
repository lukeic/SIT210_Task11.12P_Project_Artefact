#define CAMERA_MODEL_AI_THINKER

#include "esp_camera.h"
#include "camera_pins.h"
#include <TJpg_Decoder.h>
#include <SPI.h>
#include <TFT_eSPI.h>
#include <WiFi.h>
#include <HTTPClient.h>
#include <ArduinoJson.h>
#include <atomic>
#include <esp_pthread.h>
#include <esp_task_wdt.h>

const char *WIFI_SSID = "";
const char *WIFI_PASSWORD = "";
const unsigned int WIFI_CONNECTION_TIMEOUT = 15000;
const unsigned int MAX_HTTP_REQUEST_RETRIES = 5;
const unsigned int WEATHER_FORECAST_INTERVAL = 15000;
const unsigned int PIN_PUSH_BUTTON = 4;
const unsigned int ROTATION_LANDSCAPE = 1;
const unsigned int BTN_DEBOUNCE_TIME = 50;

int lastButtonState = LOW;
unsigned long lastDebounceTime = 0;
unsigned long lastWeatherForecast = 0;
bool isCameraShutterEnabled = true;
String weatherForecast = "";
String error = "";

int buttonState;
TaskHandle_t weatherForecastTask;

// Initialise TFT display with TFT library
TFT_eSPI tft = TFT_eSPI();

// Write text to TFT display
void writeMessage(const String &message)
{
	tft.setCursor(5, 5);
	tft.setTextSize(1);
	tft.setTextWrap(true);
	tft.print(message);
}

// Write multi-line text to TFT display
void writeMessageLn(const String &message)
{
	tft.setCursor(5, 5);
	tft.setTextSize(1);
	tft.setTextWrap(true);
	tft.println(message);
}

// A JPEG image is broken down into 8x8 pixel blocks called MCUs;
// this procedure renders MCU (Minimum Coded Unit) blocks to the TFT screen.
// It is called by the JPEG decoding library once an MCU has been decoded.
bool drawMcuBlock(int16_t x, int16_t y, uint16_t w, uint16_t h, uint16_t *bitmap)
{
	if (y >= tft.height())
	{
		return false;
	}

	tft.pushImage(x, y, w, h, bitmap);

	return true;
}

// Frees memory used by a photo buffer created by the ESP32's camera.
// Wrapping the 'esp_camera_fb_return' procedure makes my code more readable.
void freePhotoResources(camera_fb_t *photo)
{
	esp_camera_fb_return(photo);
}

// Attempts to connect to a WiFi connection.
// Returns true or false depending on success.
bool tryConnectToWifi()
{
	unsigned long startingTime = millis();
	WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

	while (WiFi.status() != WL_CONNECTED)
	{
		delay(500);
		if ((millis() - startingTime) > WIFI_CONNECTION_TIMEOUT)
		{
			return false;
		}
	}

	return true;
}

// Uses the ESP32-CAM's camera to take a photo and store it in an in-memory buffer.
// Returns nullptr if taking a photo failed.
camera_fb_t *takePhoto()
{
	camera_fb_t *photo = esp_camera_fb_get();
	if (!photo || photo->format != PIXFORMAT_JPEG)
	{
		writeMessage("Failed to take photo");
		freePhotoResources(photo);
		return nullptr;
	}

	return photo;
}

// Main procedure for identifying an image of a plant.
// Sends in-memory image to the Natura API and prints the results to the TFT screen.
// Updates global error on failure.
void identifyPlant(camera_fb_t *fb)
{
	HTTPClient client;
	client.begin("http://natura.azurewebsites.net/api/identify");
	client.addHeader("Content-Type", "application/octet-stream");
	int httpStatus = client.POST(fb->buf, fb->len);

	String response;
	switch (httpStatus)
	{
		case HTTP_CODE_OK:
			response = client.getString();
			break;
		case HTTP_CODE_NOT_FOUND:
			error = "Plant not found";
			break;
		default:
			error = "Server Error";
			Serial.print("Natura API error with status:");
			Serial.println(httpStatus);
			break;
	}

	DynamicJsonDocument doc(1024);
	deserializeJson(doc, response);
	JsonArray plantNames = doc.as<JsonArray>();

	Serial.println("Identified plant with name(s):");

	int yPos = 4;
	for (String plantName : plantNames)
	{
		Serial.println(plantName);
		writeMessageLn(plantName);
		yPos += 16;
	}

	client.end();
	WiFi.disconnect();
}

// Procedure to coordinate plant identification.
// 1. Takes a photo
// 2. Connects to WiFi
// 3. Sends image to Natura API
// 4. Cleans-up memory resources
void takePhotoAndIdentify()
{
	camera_fb_t *photo = takePhoto();
	if (!photo)
	{
		return;
	}

	TJpgDec.drawJpg(0, 0, (const uint8_t *) photo->buf, photo->len);
	writeMessage("Connecting to WiFi...");
	if (tryConnectToWifi())
	{
		writeMessage("WiFi Active");
		TJpgDec.drawJpg(0, 0, (const uint8_t *) photo->buf, photo->len);
		identifyPlant(photo);
	} else
	{
		writeMessage("Failed to connect to WiFi");
	}

	freePhotoResources(photo);
}

// Event loop to handle a button press.
// Uses debouncing to prevent excessive presses of the button and API calls.
void handleButtonPress()
{
	int newButtonState = digitalRead(PIN_PUSH_BUTTON);
	if (newButtonState != lastButtonState)
	{
		lastDebounceTime = millis();
	}

	bool isDebounced = (millis() - lastDebounceTime) < BTN_DEBOUNCE_TIME;
	if (!isDebounced && newButtonState != buttonState)
	{
		buttonState = newButtonState;

		if (buttonState == HIGH)
		{
			isCameraShutterEnabled = !isCameraShutterEnabled;

			if (!isCameraShutterEnabled)
			{
				takePhotoAndIdentify();
			}
		}
	}

	lastButtonState = newButtonState;
}

// Main procedure to 'stream' the camera view to the TFT display.
// Works by simply taking a photo and drawing it to the screen.
// Calling this in the main loop emulates a continual camera stream.
void streamCameraToDisplay()
{
	if (!isCameraShutterEnabled)
	{
		return;
	}

	camera_fb_t *photo = takePhoto();
	if (!photo)
	{
		return;
	}

	TJpgDec.drawJpg(0, 0, (const uint8_t *) photo->buf, photo->len);
	freePhotoResources(photo);
}

// Weather forecast procedure. Runs in a loop on a separate thread to the main loop.
// Uses locally-scoped lambda functions to keep all weather forecast functionality in one place,
// to ease readability.
[[noreturn]] void getWeatherForecast(void *parameter)
{
	for (;;)
	{
		unsigned int retryCount = 0;

		// Processes API responses based on their HTTP status.
		// Updates global error if API call failed.
		auto tryDeserialiseHttpResponse = [](int httpStatus, HTTPClient *httpClient)
		{
			String response;

			Serial.print("Processing HTTP response with status code ");
			Serial.println(httpStatus);

			switch (httpStatus)
			{
				case HTTP_CODE_OK:
					response = httpClient->getString();
					break;
				case HTTP_CODE_NOT_FOUND:
					error = "404 Not Found";
					break;
				default:
					error = "Server Error";
					break;
			}

			return response;
		};

		// Cleans up resources used when making API calls.
		auto cleanup = [&](HTTPClient *client, const String &error)
		{
			if (!error.isEmpty())
			{
				Serial.println(error);
			}
			if (client != nullptr)
			{
				client->end();
			}
			WiFi.disconnect();
			yield();
		};

		// Retrieves the city the device is currently being used in.
		// Uses a free geo-location API provided by ip-api.com, which uses the device's IP address to locate it.
		//
		// The city is used in a subsequent call to a weather API.
		auto getCity = [&]()
		{
			HTTPClient client;
			DynamicJsonDocument jsonDocument(1024);

			client.setTimeout(15000);
			client.begin("http://ip-api.com/json/?fields=city");
			int httpStatus = client.GET();
			String response = tryDeserialiseHttpResponse(httpStatus, &client);
			if (response.isEmpty())
			{
				cleanup(&client, "Failed to forecast weather: geolocation API error");
			}

			deserializeJson(jsonDocument, response);

			JsonObject location = jsonDocument.as<JsonObject>();
			String city = location["city"].as<String>();
			if (city.isEmpty())
			{
				cleanup(&client, "Failed to forecast weather: city not found");
			}

			client.end();

			return city;
		};

		// Retrieves hourly-forecast data for a city.
		// Uses a free API provided by openweathermap.org.
		//
		// Returns forecast info such as 'Cloudy', 'Drizzle', 'Clear' etc.
		auto getWeatherForecastForCity = [&](const String &city)
		{
			String result;
			HTTPClient client;
			DynamicJsonDocument jsonDocument(1024);

			String url =
				"http://api.openweathermap.org/data/2.5/forecast?APPID={API KEY}&cnt=1&q=" +
				city;

			client.begin(url);
			Serial.println("Querying weather API at " + url);
			int httpStatus = client.GET();

			while (httpStatus == HTTP_CODE_MOVED_PERMANENTLY ||
				   httpStatus == HTTP_CODE_TEMPORARY_REDIRECT ||
				   httpStatus == HTTP_CODE_PERMANENT_REDIRECT)
			{
				retryCount++;
				if (retryCount > MAX_HTTP_REQUEST_RETRIES)
				{
					cleanup(&client, "Failed to forecast weather: exceeded max retry count when trying to redirect");
					return result;
				}

				String newLocation = client.header("Location");
				if (newLocation.isEmpty())
				{
					cleanup(&client, "Failed to forecast weather: API redirected without providing a new location");
					return result;
				} else
				{
					client.end();
					Serial.println("Querying weather API at " + newLocation);
					client.begin(newLocation);
					httpStatus = client.GET();
				}
			}

			retryCount = 0;

			String response = tryDeserialiseHttpResponse(httpStatus, &client);
			if (response.isEmpty())
			{
				cleanup(&client, "Failed to forecast weather: weather API error");
				return result;
			}

			Serial.println("Got weather data" + response);
			deserializeJson(jsonDocument, response);

			JsonObject forecastData = jsonDocument.as<JsonObject>();
			JsonObject forecast = forecastData["list"].as<JsonArray>()[0].as<JsonObject>();
			JsonObject weather = forecast["weather"].as<JsonArray>()[0].as<JsonObject>();
			String weatherStatus = weather["main"].as<String>();
			Serial.println("WEATHER STATUS: " + weatherStatus);
			result = weatherStatus;

			cleanup(&client, String());

			return result;
		};

		// Main loop functionality.
		// Calls the above lambdas to find the city the device is in, and then forecast data for this city.
		//
		// Updates a global 'weatherForecast' variable which is used by the main loop to print forecast data
		// to screen.
		bool isTimeForWeatherForecast = (millis() - lastWeatherForecast) > WEATHER_FORECAST_INTERVAL;
		if (isTimeForWeatherForecast)
		{
			if (!tryConnectToWifi())
			{
				error = "Wifi Down";
				cleanup(nullptr, error);
			}

			String city = getCity();
			String forecast = getWeatherForecastForCity(city);
			if (!forecast.isEmpty())
			{
				weatherForecast = forecast;
			}

			cleanup(nullptr, String());
			lastWeatherForecast = millis();
		} else
		{
			// Yield CPU time to main thread.
			// Not doing this will stall the main thread and trigger the ESP32's 'task watchdog' which
			// will crash the program if it detects resource-hungry tasks.
			delay(10);
			yield();
		}
	}
}

void setup()
{
	Serial.begin(115200);
	delay(1000);

	pinMode(PIN_PUSH_BUTTON, INPUT);

	Serial.println("INIT DISPLAY");
	tft.begin();
	tft.setRotation(ROTATION_LANDSCAPE);
	tft.setTextColor(0xFFFF, 0x0000);
	tft.fillScreen(TFT_BLACK);

	TJpgDec.setJpgScale(1);
	TJpgDec.setSwapBytes(true);
	TJpgDec.setCallback(drawMcuBlock);

	// Generic pin setup for the ESP32 MCU.
	// Same for most ESP32s.
	Serial.println("INIT CAMERA");
	camera_config_t config;
	config.ledc_channel = LEDC_CHANNEL_0;
	config.ledc_timer = LEDC_TIMER_0;
	config.pin_d0 = Y2_GPIO_NUM;
	config.pin_d1 = Y3_GPIO_NUM;
	config.pin_d2 = Y4_GPIO_NUM;
	config.pin_d3 = Y5_GPIO_NUM;
	config.pin_d4 = Y6_GPIO_NUM;
	config.pin_d5 = Y7_GPIO_NUM;
	config.pin_d6 = Y8_GPIO_NUM;
	config.pin_d7 = Y9_GPIO_NUM;
	config.pin_xclk = XCLK_GPIO_NUM;
	config.pin_pclk = PCLK_GPIO_NUM;
	config.pin_vsync = VSYNC_GPIO_NUM;
	config.pin_href = HREF_GPIO_NUM;
	config.pin_sscb_sda = SIOD_GPIO_NUM;
	config.pin_sscb_scl = SIOC_GPIO_NUM;
	config.pin_pwdn = PWDN_GPIO_NUM;
	config.pin_reset = RESET_GPIO_NUM;
	config.xclk_freq_hz = 10000000;
	config.pixel_format = PIXFORMAT_JPEG;

	// Taken from the ESP32-CAM examples at https://github.com/espressif/arduino-esp32
	// "init with high specs to pre-allocate larger buffers"
	if (psramFound())
	{
		config.frame_size = FRAMESIZE_QVGA; // 320x240
		config.jpeg_quality = 10;
		config.fb_count = 2;
	} else
	{
		config.frame_size = FRAMESIZE_SVGA;
		config.jpeg_quality = 12;
		config.fb_count = 1;
	}

	esp_err_t err = esp_camera_init(&config);
	if (err != ESP_OK)
	{
		Serial.printf("Camera init failed with error 0x%x", err);
		return;
	}

	// Creation of new thread running on one of the ESP32's two CPU cores.
	// Used for the weather forecast loop.
	xTaskCreatePinnedToCore(getWeatherForecast, "getWeatherForecast", 10000, NULL, 1, &weatherForecastTask, 0);
}

void loop()
{
	// Wait for button press to take a photo
	handleButtonPress();

	// Print weather forecast to screen if forecast data is available.
	if (isCameraShutterEnabled && !weatherForecast.isEmpty())
	{
		writeMessage(weatherForecast);
		delay(3000);
		weatherForecast = "";
	} else if (!error.isEmpty())
	{
		// Prints any errors to screen.
		writeMessage(error);
		delay(3000);
		error = "";
	} else
	{
		streamCameraToDisplay();
	}
}
