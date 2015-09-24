using System;
using System.IO;

namespace pogoda {
	class MainClass {
		public static void Main (string[] args) {
			String city;
			int days;
			int okresMin;
			int cityId;
			BackgroundTask bt1;
			System.Threading.Thread t1;
			String choice = "t";

			Console.WriteLine ("Program pobierajacy i analizujacy dane pogodowe pochodzace z serwisu openweathermap.org");	

			do {
				try {
					Console.Write ("\nProsze wpisac nazwe miasta (bez polskich znakow): ");
					city = Console.ReadLine();
					Console.Write ("Prosze wpisac liczbe dni, ktore ma obejmowac prognoza <1..16>: ");
					days = Int32.Parse(Console.ReadLine());
					Console.Write ("Prosze wpisac okres sprawdzania pogody [min]: ");
					okresMin = Int32.Parse(Console.ReadLine());
				} catch {
					Console.WriteLine ("Bledne dane"); continue;
				}

				try {
					cityId = getCityID (city, "../../dane/city.list.json"); // lista pobrana z http://bulk.openweathermap.org/sample/city.list.json.gz
				} catch {
					Console.WriteLine ("Nie znaleziono bazy miast w folderze /dane/city.list.json"); break;
				}

				if (cityId == 0) {
					Console.WriteLine ("Nie mozna znalezc miasta"); continue;
				}

				Console.WriteLine ("ID miasta to: " + cityId);
				Console.WriteLine ("Aby przerwac nacisnij enter...");
				Console.WriteLine ();

				bt1 = new BackgroundTask(cityId, days, okresMin);
				t1 = new System.Threading.Thread(new System.Threading.ThreadStart(bt1.keepChecking));
				t1.Start();

				//while (!t1.IsAlive);
				Console.ReadLine();


				Console.WriteLine ("Czy powtorzyc program dla innych kryteriow? [t/n]");
				choice = Console.ReadLine();
				t1.Abort (); t1.Join ();
			} while (choice.ToLower().Equals("t"));

			Console.WriteLine ("Koniec programu.");
		}
			


		/////////// metody pomocnicze

		public static double averageDelta(Temp tempAvg) {
			//Console.WriteLine ("metoda averageDelta()");
			//Console.WriteLine ("averageDelta: " + (tempAvg.max - tempAvg.min));
			//Console.WriteLine ("koniec metody averageDelta()");
			return tempAvg.max - tempAvg.min;
		}


		public static Temp averageTempDays(WeatherData wd1) { // liczenie sredniej		
			//Console.WriteLine ("metoda averageTempDays()");

			Temp temp1 = new Temp();

			//day
			foreach (Days d in wd1.list)
				temp1.day += d.temp.day;
			temp1.day /= wd1.list.Length;
			//Console.WriteLine ("avg day: " + temp1.day);

			//min
			foreach (Days d in wd1.list)
				temp1.min += d.temp.min;
			temp1.min /= wd1.list.Length;
			//Console.WriteLine ("avg min: " + temp1.min);

			//max
			foreach (Days d in wd1.list)
				temp1.max += d.temp.max;
			temp1.max /= wd1.list.Length;
			//Console.WriteLine ("avg max: " + temp1.max);

			//night
			foreach (Days d in wd1.list)
				temp1.night += d.temp.night;
			temp1.night /= wd1.list.Length;
			//Console.WriteLine ("avg night: " + temp1.night);

			//eve
			foreach (Days d in wd1.list)
				temp1.eve += d.temp.eve;
			temp1.eve /= wd1.list.Length;
			//Console.WriteLine ("avg eve: " + temp1.eve);

			//morn
			foreach (Days d in wd1.list)
				temp1.morn += d.temp.morn;
			temp1.morn /= wd1.list.Length;
			//Console.WriteLine ("avg morn: " + temp1.morn);

			//Console.WriteLine ("koniec metody averageTempDays()");
			return temp1;
		}


		public static Temp convertKelvinToCelsius(Temp temp) {
			//Console.WriteLine ("metoda convertKelvinToCelsius()");
			Temp temp1 = new Temp ();

			temp1.day = temp.day - 273.15;
			temp1.min = temp.min - 273.15;
			temp1.max = temp.max - 273.15;
			temp1.night = temp.night - 273.15;
			temp1.eve = temp.eve - 273.15;
			temp1.morn = temp.morn - 273.15;
		
			//Console.WriteLine ("koniec metody convertKelvinToCelsius()");
			return temp1;
		}


		public static WeatherData getWeatherDataFromJson(String weatherJson) {
			//Console.WriteLine ("metoda getWeatherDataFromJson()");

			WeatherData wd1 = Newtonsoft.Json.JsonConvert.DeserializeObject<WeatherData> (weatherJson); // deserializacja linii json	

			//Console.WriteLine ("koniec metody getWeatherDataFromJson()");
			return wd1;
		}


		public static String getForecast(int cityId, int days) { // api.openweathermap.org/data/2.5/forecast/daily?id={city ID}&cnt={cnt}; id: city ID; cnt: number of days returned (from 1 to 16)
			//Console.WriteLine ("metoda getForecast()");
			string htmlCode;

			using (System.Net.WebClient client = new System.Net.WebClient ()) 
			{
				client.QueryString.Add("id", cityId.ToString()); 
				client.QueryString.Add("cnt", days.ToString()); 
				htmlCode = client.DownloadString("http://api.openweathermap.org/data/2.5/forecast/daily");
			}

			//Console.WriteLine (htmlCode);
			//Console.WriteLine ("koniec metody getForecast()");
			return htmlCode;
		}


		public static int getCityID(String cityName, String listPath) { 
			//Console.WriteLine ("metoda getCityID()");

			int counter = 0;
			string line; // znaleziona linia json zawierajaca szukane miasto
			City c1 = null;

			System.IO.StreamReader file =  new System.IO.StreamReader(listPath); // wczytywanie pliku listy miast
			while((line = file.ReadLine()) != null) {
				line = line.Replace ("_id", "id"); // specyfikacja API jest niescisla w sprawie _id vs id
				if ((line.ToLower()).Contains ("\"" + cityName.ToLower() + "\"")) {
					Console.WriteLine (line);
					break;
				}
				counter++;
			}
			file.Close();

			if (line == null || line == String.Empty) {
				// Console.WriteLine ("line == null!");
				return 0;
			} else {
				c1 = Newtonsoft.Json.JsonConvert.DeserializeObject<City> (line); // deserializacja linii json	
			}


			//Console.WriteLine ("koniec metody getCityID()");
			return c1.id;
		}
	}
		


	/////////// klasa w tle

	public class BackgroundTask {
		int cityId;
		int days;
		int okresMinut;

		public BackgroundTask(int cityId, int days, int okresMinut) {
			this.cityId = cityId;
			this.days = days;
			this.okresMinut = okresMinut;
		}


		public void keepChecking()
		{
			String weatherDataJson;
			String dataCzas;
			String nazwaPliku;

			string[] fileArray = Directory.GetFiles(Directory.GetCurrentDirectory(), cityId + "_" + days + "_*.json");
			foreach (string s in fileArray) {
				string danePogodoweJson = File.ReadAllText(s);

				WeatherData danePogodowe = MainClass.getWeatherDataFromJson (danePogodoweJson);
				Temp srednieTemperatury = MainClass.averageTempDays (danePogodowe);
				srednieTemperatury = MainClass.convertKelvinToCelsius (srednieTemperatury);

				double sredniaRoznica = MainClass.averageDelta (srednieTemperatury);

				string dataPliku = (new System.Text.RegularExpressions.Regex (@"/.*/.+?_.+?_|\.json")).Replace(s, "");
				Console.WriteLine (dataPliku);
				Console.WriteLine ("srednie wartosci temperatur:");
				Console.WriteLine (srednieTemperatury.ToString());
				Console.WriteLine ("srednia roznica min-max: " ); 
				Console.WriteLine (sredniaRoznica);
				Console.WriteLine ();
			}

			while (true)
			{
				try {
					weatherDataJson = MainClass.getForecast (cityId, days);
					dataCzas = DateTime.Now.ToString (new System.Globalization.CultureInfo("en-GB")).Replace("/", "-").Replace(" ", "_").Replace(":", "-");
					nazwaPliku = cityId + "_" + days + "_" + dataCzas + ".json";
					System.IO.File.WriteAllText(nazwaPliku, weatherDataJson);

					WeatherData danePogodowe = MainClass.getWeatherDataFromJson (weatherDataJson);
					Temp srednieTemperatury = MainClass.averageTempDays (danePogodowe);
					srednieTemperatury = MainClass.convertKelvinToCelsius (srednieTemperatury);

					double sredniaRoznica = MainClass.averageDelta (srednieTemperatury);

					string dataPliku = (new System.Text.RegularExpressions.Regex (@"/.*/.+?_.+?_|\.json|"+cityId+"_.+?_")).Replace(nazwaPliku, "");
					Console.WriteLine (dataPliku);
					Console.WriteLine ("srednie wartosci temperatur:");
					Console.WriteLine (srednieTemperatury.ToString());
					Console.WriteLine ("srednia roznica min-max: " ); 
					Console.WriteLine (sredniaRoznica);
					Console.WriteLine ();

				} catch {
					Console.WriteLine ("Brak polaczenia z serwerem");
					System.Threading.Thread.Sleep(okresMinut*1000);
				}
				System.Threading.Thread.Sleep(okresMinut*1000*60);
			}
		}
	};


	/////////// klasy pomocnicze do parsowania json

	public class Coord
	{
		public double lon { get; set; }
		public double lat { get; set; }
	}

	public class City
	{
		public int id { get; set; }
		public string name { get; set; }
		public Coord coord { get; set; }
		public string country { get; set; }
	}

	public class Temp
	{
		public double day { get; set; }
		public double min { get; set; }
		public double max { get; set; }
		public double night { get; set; }
		public double eve { get; set; }
		public double morn { get; set; }

		public override string ToString() {
			return String.Format("rano: {0}\ndzien: {1}\nwieczor: {2}\nnoc: {3}\nmin: {4}\nmax: {5}", morn, day, eve, night, min, max);
		}
	}

	public class Weather
	{
		public int id { get; set; }
		public string main { get; set; }
		public string description { get; set; }
		public string icon { get; set; }
	}

	public class Days
	{
		public int dt { get; set; }
		public Temp temp { get; set; }
		public double pressure { get; set; }
		public int humidity { get; set; }
		public Weather[] weather { get; set; }
	}

	public class WeatherData
	{
		public string cod { get; set; }
		public double message { get; set; }
		public City city { get; set; }
		public int cnt { get; set; }
		public Days[] list { get; set; }
	}
}