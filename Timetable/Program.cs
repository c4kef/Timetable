/*
 * A lot of rubbish, but I was too lazy to write this
 */
using VkNet;
using VkNet.Model;
using VkNet.Model.RequestParams;
using VkNet.Enums.SafetyEnums;
using VkNet.Enums.Filters;
using System;
using System.IO;
using VkNet.Model.GroupUpdate;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using HtmlAgilityPack;
using System.Globalization;
using System.Text;

namespace Timetable
{
	[Serializable]
	class Lessons
	{
		public struct LessonsStruct
		{
			public LessonsStruct(string name, string cabinet, string homework, string time)
			{
				this.name = name;
				this.cabinet = cabinet;
				this.homework = homework;
			}
			public string name;
			public string cabinet;
			public string homework;
		}

		public List<LessonsStruct> evenLessons, oddLessons;
		public List<string> timeLessons;
	}
	public struct CookieAuth
	{
		public string name;
		public string link;
		public string login;
		public string password;
		public string phpsessionid;
	}

	public struct MarkChenger
	{
		public string markId;
		public string mark;
	}

	[Serializable]
	class AuthData
	{
		public List<CookieAuth> auths;
	}

	class Program
	{
		private struct DateInformation
		{
			public int id;
			public string dateText;
			public bool dateEven;
			public DayOfWeek dateEnum;
		}

		public static VkApi Api;

		public static string MyAppToken = "-";
		public static ulong MyGroupId = 0;

		public static Lessons GetLessons;
		public static AuthData GetAuth;
		public static bool isBusy;

		static void Main(string[] args)
		{
			Api = new VkApi();
			GetAuth = JsonConvert.DeserializeObject<AuthData>(File.ReadAllText("auth.json"));
			new Thread(new ThreadStart(() =>
			{
				Api.Authorize(new ApiAuthParams
				{
					AccessToken = MyAppToken
				});
				Api.SetLanguage(VkNet.Enums.Language.Ru);

				var s = Api.Groups.GetLongPollServer(MyGroupId);
				int countError = 0;

				var list = new List<long>();

				while (true)
				{
					try
					{
						var poll = Api.Groups.GetBotsLongPollHistory(new BotsLongPollHistoryParams() { Server = s.Server, Ts = s.Ts, Key = s.Key, Wait = 25 });

						countError = 0;
						if (poll?.Updates == null) continue;
						foreach (var a in poll.Updates)
						{
							if (a.Type == GroupUpdateType.MessageNew)
							{
								s.Ts = poll.Ts;
								if (string.IsNullOrEmpty(a.MessageNew.Message.Text))
									continue;
								Console.WriteLine(a.MessageNew.Message.PeerId);
								string userMessage = a.MessageNew.Message.Text.ToLower();
								string userMessageOriginal = a.MessageNew.Message.Text;
								string answer = "";
								if (userMessage.Contains("уроки") || (userMessage.Contains("расписание")))
								{
									DateInformation date = GetInfo(userMessage);
									if (date.id < 0)
									{
										Send("Сенпай, я не совсем поняла, на какую дату хочешь уроки? ❤️", a);
										continue;
									} else if (((date.dateEven) ? GetLessons.evenLessons.Count : GetLessons.oddLessons.Count) <= date.id)
									{
										Send("Мой создатель не добавил уроков на этот день (>_<)❤️", a);
										continue;
									}

									answer = $"Расписание на {date.dateText} сенпай ❤️:\nНеделя: {(date.dateEven ? "чётная" : "нечётная")}\n";

									for (int i = 0; i <= 4; i++)
									{
										Lessons.LessonsStruct lesson = (date.dateEven) ? GetLessons.evenLessons[date.id + i] : GetLessons.oddLessons[date.id + i];
										if (lesson.name != "-")
											answer += $"{i + 1}. {lesson.name}:\n - Время: {GetLessons.timeLessons[i]}\n - Кабинет: {lesson.cabinet}\n - Домашка: {lesson.homework}\n\n";
										else
											answer += $"{i + 1}. {lesson.name}:\n - Время: {GetLessons.timeLessons[i]}\n\n";
									}
								} else if (userMessage.Contains("помощь") || userMessage.Contains("контакты"))
								{
									Send("Вот что я на данный момент умею, сенпай ❤️:\n- зачёт (договоримся лично, в бота лень встраивать)\n- уроки \"день недели\"\n- !Оценка \"Предмет\" \"Фамилия\" \"Индекс\" \"Оценка\" \"Месяц\"\n- !Индекс \"Предмет\" \"Фамилия\" \"Месяц\"(получить индексы для работы)\n\nКонтакты:\n- Номер куратора: 8 910 494-00-73", a);
								} else if (userMessageOriginal[0] == '!' && a.MessageNew.Message.FromId != 24441144)
								{
									if (userMessageOriginal.Contains("Оценка"))
									{
										if (isBusy)
										{
											Send($"Сенпай, я занята ^_^", a);
											continue;
										}

										string[] request = userMessageOriginal.Split(' ');
										if (request.Length < 5)
										{
											Send($"Сенпай, я не могу такое сделать (￣ω￣)", a);
											continue;
										}

										Send($"Сенпай, я приняла твой запросик, ожидай ^_^", a);

										string lesson = request[1];
										string userName = request[2];
										string index = request[3];
										isBusy = true;
										new Thread(new ThreadStart(() =>
										{
											try
											{
												string mark = GenerationMark(request[4], a.MessageNew.Message.FromId == 273020380 && userMessageOriginal.Contains("!!"));
												string month = (request.Length == 6) ? DateTime.ParseExact(request[5], "MMMM", new CultureInfo("ru-RU")).Month.ToString("00") : DateTime.Now.Month.ToString("00");
												int id = int.Parse(index);
												CookieAuth? auth = GetAuth.auths.Find(authData => authData.name.Contains(lesson));

												if (auth == null)
												{
													Send($@"Сенпай, я не нашла предмет, может создатель забыл добавить его? (-_\\\)", a);
													isBusy = false;
													return;
												}

												var r = new PHPModule(auth?.link + $"&dateon=2021-{month}-01&dateoff=2021-{month}-31");
												r.SetCookie(auth);
												string firstCheck = r.Send();
												List<MarkChenger> marks = Parser(firstCheck, userName);

												if (id > marks.Count())
												{
													Send($"Сенпай, я не смогла найти подобный идентификатор (>_<)\nP.s воспользуйся !Индекс \"Предмет\" \"Фамилия\" для получения идентификатора ^_^", a);
													isBusy = false;
													return;
												}

												r.Add(marks[id - 1].markId, mark);
												r.Add("mark", "mark");

												if (r.Send().Contains(userName))
													Send($"Сенпай, я выполнила твою команду и поставила {mark} -^o^-", a);
												else
													Send($"Сенпай, пни создателя, пусть ключи сессии обновит (>_<)", a);
											}
											catch
											{
												Send($"Сенпай, я не смогла выполнить команду (>_<)", a);
											}
											isBusy = false;
										})).Start();
									} else if (userMessageOriginal.Contains("Индекс"))
									{
										if (isBusy)
										{
											Send($"Сенпай, я занята ^_^", a);
											continue;
										}

										string[] request = userMessageOriginal.Split(' ');
										if (request.Length < 3)
										{
											Send($"Сенпай, я не могу такое сделать (￣ω￣)", a);
											continue;
										}

										Send($"Сенпай, я приняла твой запросик, ожидай ^_^", a);

										string lesson = request[1];
										string userName = request[2];
										isBusy = true;
										new Thread(new ThreadStart(() =>
										{
											try
											{
												string month = (request.Length == 4) ? DateTime.ParseExact(request[3], "MMMM", new CultureInfo("ru-RU")).Month.ToString("00") : string.Empty;
												CookieAuth? auth = GetAuth.auths.Find(authData => authData.name.Contains(lesson));

												if (auth == null)
												{
													Send($@"Сенпай, я не нашла предмет, может создатель забыл добавить его? (-_\\\)", a);
													isBusy = false;
													return;
												}
												var r = new PHPModule(auth?.link + $"&dateon=2021-{month}-01&dateoff=2021-{month}-31");
												r.SetCookie(auth);
												string firstCheck = r.Send();
												List<MarkChenger> marks = Parser(firstCheck, userName);

												if (marks.Count < 1)
												{
													Send($@"Сенпай, я не могу собрать список твоих оценок по предмету... (-_\\\)", a);
													isBusy = false;
													return;
												}

												StringBuilder builder = new StringBuilder();
												for (int i = 0; i < marks.Count; i++)
													builder.AppendLine($"{i + 1}: {(string.IsNullOrWhiteSpace(marks[i].mark) ? "-" : marks[i].mark)}");

												Send($"Сенпай, принимай индексы с оценками по {lesson} ^_^ (самый нижний считается как сегодняшний день):\n{builder.ToString()}", a);
											}
											catch
											{
												Send($"Сенпай, я не смогла выполнить команду (>_<)", a);
											}
											isBusy = false;
										})).Start();
									}
								}

								if (string.IsNullOrEmpty(answer))
									continue;

								Send(answer, a);
							}
						}
					}
					catch (Exception ex)
					{
						if (countError++ < 1)
							s = Api.Groups.GetLongPollServer(MyGroupId);

						Console.WriteLine($"Error: {ex.Message}");
					}
				}
			})).Start();//2000000006 - Кумарные войска

			Console.WriteLine($"-> Bot is started...");

			while (true)
			{
				string command = Console.ReadLine();
				switch (command)
				{
					case "reloadLessons":
						GetLessons = JsonConvert.DeserializeObject<Lessons>(File.ReadAllText(@"lessons.json"));
						Console.WriteLine("-> Successful update");
						break;
					case "reloadAuths":
						GetAuth = JsonConvert.DeserializeObject<AuthData>(File.ReadAllText("auth.json"));
						Console.WriteLine("-> Successful update");
						break;
					default:
						Console.WriteLine($"-> Command \"{command}\" not found!");
						break;
				}
			}
		}

		private static bool IsEvenWeek(DateTime date) => CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(date, CalendarWeekRule.FirstDay, date.DayOfWeek) % 2 == 0;

		public static DateTime GetNextWeekday(DayOfWeek day)
		{
			DateTime result = DateTime.Now.AddDays(1);
			while (result.DayOfWeek != day)
				result = result.AddDays(1);
			return result;
		}

		private static List<MarkChenger> Parser(string response, string userName)
		{
			List<MarkChenger> lsCollection = new List<MarkChenger>();
			var doc = new HtmlDocument();
			doc.LoadHtml(response);
			HtmlNodeCollection collection = doc.DocumentNode.
				ChildNodes[2].
				ChildNodes[3].
				ChildNodes[11]
				.ChildNodes[1]
				.ChildNodes[5]
				.ChildNodes[1]
				.ChildNodes[1]
				.ChildNodes[1]
				.ChildNodes[4]
				.ChildNodes[1]
				.ChildNodes[1]
				.ChildNodes[1]
				.ChildNodes[1]
				.ChildNodes[1]
				.ChildNodes;

			for (int j = 0; j < collection.Count; j++)
				if (collection[j].InnerHtml.Contains(userName))
					for (int i = 0; i < collection[j].ChildNodes.Count; i++)
					{
						try
						{
							var c = collection[j].ChildNodes[i - 2].ChildNodes[0];
							lsCollection.Add(new MarkChenger() { markId = collection[j].ChildNodes[i - 2].ChildNodes[0].Attributes[3].Value, mark = collection[j].ChildNodes[i - 2].ChildNodes[0].ChildNodes[1].InnerHtml });//$"{collection[j].ChildNodes[i - 2].ChildNodes[0].Attributes[3].Value} - {collection[j].ChildNodes[i - 2].ChildNodes[0].ChildNodes[1].InnerHtml}" });
						}
						catch { }
					}

			return lsCollection;
		}

		private static void Send(string msg, GroupUpdate a)
		{
			MessagesSendParams msg_chat = new MessagesSendParams()
			{
				PeerId = a.MessageNew.Message.PeerId,
				Message = msg,
				RandomId = new Random().Next(0, 1000000000)
			};

			Api.Messages.Send(msg_chat);
		}

		private static string GenerationMark(string req, bool isAdmin)
		{
			if (isAdmin)
				return req;

			if (req.ToLower().Contains("нб"))
				return "н/б";
			else if (req.ToLower().Contains("стереть"))
				return " ";
			else if (req.ToLower().Contains("2"))
				return "2";
			else if (req.ToLower().Contains("3"))
				return "3";
			else if (req.ToLower().Contains("4"))
				return "4";
			else if (req.ToLower().Contains("5"))
				return "5";

			return " ";
		}

		private static DateInformation GetInfo(string dateParse)
		{
			DayOfWeek dayEnum = DayOfWeek.Monday;
			string dayText = string.Empty;
			int dayId = 0;
			bool dayEven = false;

			if (dateParse.Contains("завтра"))
			{
				dayEnum = DateTime.Now.AddDays(1).DayOfWeek;
				dayEven = IsEvenWeek(DateTime.Now.AddDays(1));
				dayText = "завтра";
			} else if (dateParse.Contains("сегодня"))
			{
				dayEnum = DateTime.Now.DayOfWeek;
				dayEven = IsEvenWeek(DateTime.Now);
				dayText = "сегодня";
			} else if (dateParse.Contains("послезавтра"))
			{
				dayEnum = DateTime.Now.AddDays(2).DayOfWeek;
				dayEven = IsEvenWeek(DateTime.Now);
				dayText = "послезавтра";
			} else if (dateParse.Contains("вчера"))
			{
				dayEnum = DateTime.Now.AddDays(-1).DayOfWeek;
				dayEven = IsEvenWeek(DateTime.Now);
				dayText = "вчера";
			} else if (dateParse.Contains("понед"))
			{
				dayEnum = DayOfWeek.Monday;
				dayEven = IsEvenWeek(DateTime.Now.DayOfWeek > dayEnum ? GetNextWeekday(dayEnum) : DateTime.Now);
				dayText = DateTime.Now.DayOfWeek > dayEnum ? "следующий понедельник" : "понедельник";
			} else if (dateParse.Contains("вторн"))
			{
				dayEnum = DayOfWeek.Tuesday;
				dayEven = IsEvenWeek(DateTime.Now.DayOfWeek > dayEnum ? GetNextWeekday(dayEnum) : DateTime.Now);
				dayText = DateTime.Now.DayOfWeek > dayEnum ? "следующий вторник" : "вторник";
			} else if (dateParse.Contains("сред"))
			{
				dayEnum = DayOfWeek.Wednesday;
				dayEven = IsEvenWeek(DateTime.Now.DayOfWeek > dayEnum ? GetNextWeekday(dayEnum) : DateTime.Now);
				dayText = DateTime.Now.DayOfWeek > dayEnum ? "следующею среду" : "среду";
			} else if (dateParse.Contains("чет"))
			{
				dayEnum = DayOfWeek.Thursday;
				dayEven = IsEvenWeek(DateTime.Now.DayOfWeek > dayEnum ? GetNextWeekday(dayEnum) : DateTime.Now);
				dayText = DateTime.Now.DayOfWeek > dayEnum ? "следующий четверг" : "четверг";
			} else if (dateParse.Contains("пят"))
			{
				dayEnum = DayOfWeek.Friday;
				dayEven = IsEvenWeek(DateTime.Now.DayOfWeek > dayEnum ? GetNextWeekday(dayEnum) : DateTime.Now);
				dayText = DateTime.Now.DayOfWeek > dayEnum ? "следующую пятницу" : "пятницу";
			} else if (dateParse.Contains("суб"))
			{
				dayEnum = DayOfWeek.Saturday;
				dayEven = IsEvenWeek(DateTime.Now.DayOfWeek > dayEnum ? GetNextWeekday(dayEnum) : DateTime.Now);
				dayText = DateTime.Now.DayOfWeek > dayEnum ? "следующую субботу" : "субботу";
			} else if (dateParse.Contains("вос"))
			{
				dayEnum = DayOfWeek.Sunday;
				dayEven = IsEvenWeek(DateTime.Now.DayOfWeek > dayEnum ? GetNextWeekday(dayEnum) : DateTime.Now);
				dayText = DateTime.Now.DayOfWeek > dayEnum ? "следующее воскресенье" : "воскресенье";
			} else
				return new DateInformation() { id = -1 };

			switch (dayEnum)
			{
				case DayOfWeek.Monday:
					dayId = 0;
					break;

				case DayOfWeek.Tuesday:
					dayId = 5;
					break;

				case DayOfWeek.Wednesday:
					dayId = 10;
					break;

				case DayOfWeek.Thursday:
					dayId = 15;
					break;

				case DayOfWeek.Friday:
					dayId = 20;
					break;

				case DayOfWeek.Saturday:
					dayId = 25;
					break;

				case DayOfWeek.Sunday:
					dayId = 30;
					break;
			}

			return new DateInformation() { id = dayId, dateEnum = dayEnum, dateText = dayText, dateEven = dayEven };
		}
	}
}
