using System;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace YourEasyBot
{
	public class YourBot : EasyBot
	{
		public static void Main_Start()
		{
			var bot = new YourBot("token");
			bot.Run();
		}

		public YourBot(string botToken) : base(botToken) { }

		public override async Task OnPrivateChat(Chat chat, User user, UpdateInfo update)
		{
			if (update.UpdateKind != UpdateKind.NewMessage || update.MsgCategory != MsgCategory.Text)
				return;
			if (update.Message.Text == "/start")
			{
				await Telegram.SendTextMessageAsync(chat, "What is your first name?");
				var firstName = await NewTextMessage(update);
				// execution continues here once we received a new text message
				await Telegram.SendTextMessageAsync(chat, "What is your last name?");
				var lastName = await NewTextMessage(update);
                InlineKeyboardButton[] buttons = new InlineKeyboardButton[]
				{
					new InlineKeyboardButton("Male") { CallbackData = "🚹" },
					new InlineKeyboardButton("Female") { CallbackData = "🚺" },
					new InlineKeyboardButton("Other") { CallbackData = "⚧" }
				};
                InlineKeyboardMarkup replyMarkup = new InlineKeyboardMarkup(buttons);
                var genderMsg = await Telegram.SendTextMessageAsync(chat, "What is your gender?", replyMarkup: replyMarkup);
                var genderEmoji = await ButtonClicked(update, genderMsg);
				ReplyCallback(update, "You clicked " + genderEmoji);
				await Telegram.SendTextMessageAsync(chat, $"Welcome, {firstName} {lastName}! ({genderEmoji})" +
					$"\n\nFor more fun, try to type /button@{BotName} in a group I'm in");
				return;
			}
		}

		public override async Task OnGroupChat(Chat chat, UpdateInfo update)
		{
			Console.WriteLine($"In group chat {chat.Name()}");
			do
			{
				switch (update.UpdateKind)
				{
					case UpdateKind.NewMessage:
						Console.WriteLine($"{update.Message.From.Name()} wrote: {update.Message.Text}");
						if (update.Message.Text == "/button@" + BotName)
							await Telegram.SendTextMessageAsync(chat, "You summoned me!", replyMarkup: new InlineKeyboardMarkup("I grant your wish"));
						break;
					case UpdateKind.EditedMessage:
						Console.WriteLine($"{update.Message.From.Name()} edited: {update.Message.Text}");
						break;
					case UpdateKind.CallbackQuery:
						Console.WriteLine($"{update.Message.From.Name()} clicked the button with data '{update.CallbackData}' on the msg: {update.Message.Text}");
						ReplyCallback(update, "Wish granted !");
						break;
				}
				// in this approach, we choose to continue execution in a loop, obtaining new updates/messages for this chat as they come
			} while (await NextEvent(update) != 0);
		}
	}
}