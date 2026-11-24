using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

// Создаем клиента для работы с Telegram-ботом
var botClient = new TelegramBotClient("8871843324:AAEuJJvjsZuEBoRADxfekVR9KgYhyc62QzU");

// Список мотивационных фраз
string[] motivationPhrases = new string[]
{
    "Ты справишься! 💪",
    "Сегодня твой день! 🌟",
    "Маленькие шаги ведут к большим победам! 🚀",
    "Не сдавайся, всё получится! ✨",
    "Ты сильнее, чем думаешь! 🔥"
};

// Здесь храним ID пользователей, от которых ждем ввод времени
HashSet<long> usersWaitingForTime = new HashSet<long>();

// Расписание: ID пользователя -> Время (сколько часов и минут)
Dictionary<long, TimeSpan> userSchedules = new Dictionary<long, TimeSpan>();

// ==== ОБРАБОТКА СЕЙЧАС (Когда пользователь пишет боту) ====
botClient.StartReceiving(
    updateHandler: async (bot, update, cancellationToken) =>
    {
        // Проверяем, что нам прислали именно текстовое сообщение
        if (update.Message == null || update.Message.Text == null)
        {
            return;
        }

        long chatId = update.Message.Chat.Id;
        string messageText = update.Message.Text.Trim();

        // 1. Проверяем команду /start
        if (messageText == "/start")
        {
            usersWaitingForTime.Add(chatId);
            await bot.SendMessage(
                chatId: chatId,
                text: "Привет! 👋\nНапиши время, в которое присылать мотивацию, в формате ЧЧ:ММ (например, 09:00 или 21:30).",
                cancellationToken: cancellationToken);
            return;
        }

        // 2. Проверяем команду /stop
        if (messageText == "/stop")
        {
            userSchedules.Remove(chatId);
            usersWaitingForTime.Remove(chatId);
            await bot.SendMessage(chatId, "Ок, больше не пишу. Чтобы возобновить — напиши /start", cancellationToken: cancellationToken);
            return;
        }

        // 3. Проверяем команду /time
        if (messageText == "/time")
        {
            if (userSchedules.TryGetValue(chatId, out TimeSpan savedTime))
            {
                await bot.SendMessage(chatId, $"Сейчас рассылка настроена на {savedTime.Hours:00}:{savedTime.Minutes:00}", cancellationToken: cancellationToken);
            }
            else
            {
                await bot.SendMessage(chatId, "Ты еще не выбрал время. Напиши /start", cancellationToken: cancellationToken);
            }
            return;
        }

        // 4. Если это не команда, проверяем, ждали ли мы от пользователя время
        if (usersWaitingForTime.Contains(chatId))
        {
            // Пытаемся превратить текст в понятное время (TimeSpan)
            if (TimeSpan.TryParse(messageText, out TimeSpan parsedTime))
            {
                userSchedules[chatId] = parsedTime;
                usersWaitingForTime.Remove(chatId); // Больше не ждем время

                await bot.SendMessage(
                    chatId: chatId,
                    text: $"Готово! Буду присылать мотивацию каждый день в {parsedTime.Hours:00}:{parsedTime.Minutes:00}.",
                    cancellationToken: cancellationToken);
            }
            else
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "Не удалось распознать время Напиши в формате ЧЧ:ММ, например 09:30",
                    cancellationToken: cancellationToken);
            }
        }
    },
    errorHandler: (bot, exception, cancellationToken) =>
    {
        Console.WriteLine($"Произошла ошибка: {exception.Message}");
        return Task.CompletedTask;
    }
);

// ==== ОБРАБОТКА В ФОНЕ (Отправка сообщений по времени) ====
_ = Task.Run(async () =>
{
    Random random = new Random();

    while (true)
    {
        // Берем текущее время (часы, минуты, секунды)
        DateTime now = DateTime.Now;

        // Перебираем всех, кто настроил расписание
        // .ToList() нужен, чтобы программа не упала, если в этот момент кто-то добавит новое время
        foreach (KeyValuePair<long, TimeSpan> item in userSchedules.ToList())
        {
            long chatId = item.Key;
            TimeSpan targetTime = item.Value;

            // Если часы и минуты совпадают с текущими
            if (now.Hour == targetTime.Hours && now.Minute == targetTime.Minutes)
            {
                // Если секунда от 0 до 20, отправляем (чтобы отправилось ровно 1 раз в эту минуту)
                if (now.Second >= 0 && now.Second < 20)
                {
                    try
                    {
                        // Выбираем случайную фразу
                        int index = random.Next(motivationPhrases.Length);
                        string phrase = motivationPhrases[index];

                        await botClient.SendMessage(chatId, phrase);
                    }
                    catch
                    {
                        // Если пользователь заблокировал бота, просто игнорируем ошибку
                    }
                }
            }
        }

        // Ждем 20 секунд перед следующей проверкой времени
        await Task.Delay(20000);
    }
});

// Держим программу запущенной
Console.WriteLine("Бот успешно запущен. Нажми Enter для закрытия...");
Console.ReadLine(); Console.WriteLine("Hello, World!");
