using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace YourEasyBot
{
	public class EasyBot	// A fun way to code Telegram Bots, by Wizou
	{
		public readonly TelegramBotClient Telegram;
		public User Me { get; private set; }
		public string BotName => Me.Username;

		private int _lastUpdateId = -1;
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        private readonly Dictionary<long, TaskInfo> _tasks = new Dictionary<long, TaskInfo>();

        public virtual Task OnPrivateChat(Chat chat, User user, UpdateInfo update) => Task.CompletedTask;
		public virtual Task OnGroupChat(Chat chat, UpdateInfo update) => Task.CompletedTask;
		public virtual Task OnChannel(Chat channel, UpdateInfo update) => Task.CompletedTask;
		public virtual Task OnOtherEvents(UpdateInfo update) => Task.CompletedTask;

        public EasyBot(string botToken)
        {
            Telegram = new TelegramBotClient(botToken);
            Me = Task.Run(() => Telegram.GetMeAsync()).Result;
        }


        public void Run() => RunAsync().Wait();
		public async Task RunAsync()
		{
			Console.WriteLine("Press Escape to stop the bot");
			while (true)
			{
				var updates = await Telegram.GetUpdatesAsync(_lastUpdateId + 1, timeout: 2);
				foreach (var update in updates)
					HandleUpdate(update);
				if (Console.KeyAvailable)
					if (Console.ReadKey().Key == ConsoleKey.Escape)
						break;
			}
			_cancel.Cancel();
		}

		public async Task<string> CheckWebhook(string url)
		{
			var webhookInfo = await Telegram.GetWebhookInfoAsync();
			string result = $"{BotName} is running";
			if (webhookInfo.Url != url)
			{
				await Telegram.SetWebhookAsync(url);
				result += " and now registered as Webhook";
			}
			return $"{result}\n\nLast webhook error: {webhookInfo.LastErrorDate} {webhookInfo.LastErrorMessage}";
		}

		/// <summary>Use this method in your WebHook controller</summary>
		public void HandleUpdate(Update update)
		{
			if (update.Id <= _lastUpdateId) return;
			_lastUpdateId = update.Id;
			switch (update.Type)
			{
				case UpdateType.Message: HandleUpdate(update, UpdateKind.NewMessage, update.Message); break;
				case UpdateType.EditedMessage: HandleUpdate(update, UpdateKind.EditedMessage, update.EditedMessage); break;
				case UpdateType.ChannelPost: HandleUpdate(update, UpdateKind.NewMessage, update.ChannelPost); break;
				case UpdateType.EditedChannelPost: HandleUpdate(update, UpdateKind.EditedMessage, update.EditedChannelPost); break;
				case UpdateType.CallbackQuery: HandleUpdate(update, UpdateKind.CallbackQuery, update.CallbackQuery.Message); break;
				case UpdateType.MyChatMember: HandleUpdate(update, UpdateKind.OtherUpdate, chat: update.MyChatMember.Chat); break;
				case UpdateType.ChatMember: HandleUpdate(update, UpdateKind.OtherUpdate, chat: update.ChatMember.Chat); break;
				default: HandleUpdate(update, UpdateKind.OtherUpdate); break;
			}
		}

        private void HandleUpdate(Update update, UpdateKind updateKind, Message message = null, Chat chat = null)
        {
            TaskInfo taskInfo;
            chat = chat ?? message?.Chat;
            long chatId = chat?.Id ?? 0;
            lock (_tasks)
                if (!_tasks.TryGetValue(chatId, out taskInfo))
                    _tasks[chatId] = taskInfo = new TaskInfo();
            var updateInfo = new UpdateInfo(taskInfo) { UpdateKind = updateKind, Update = update, Message = message };
            if (update.Type == UpdateType.CallbackQuery)
                updateInfo.CallbackData = update.CallbackQuery.Data;
            lock (taskInfo)
                if (taskInfo.Task != null)
                {
                    taskInfo.Updates.Enqueue(updateInfo);
                    taskInfo.Semaphore.Release();
                    return;
                }
            RunTask(taskInfo, updateInfo, chat);
        }

        private void RunTask(TaskInfo taskInfo, UpdateInfo updateInfo, Chat chat)
        {
            Func<Task> taskStarter;
            if (chat?.Type == ChatType.Private)
                taskStarter = () => OnPrivateChat(chat, updateInfo.Message?.From, updateInfo);
            else if (chat?.Type == ChatType.Group || chat?.Type == ChatType.Supergroup)
                taskStarter = () => OnGroupChat(chat, updateInfo);
            else if (chat?.Type == ChatType.Channel)
                taskStarter = () => OnChannel(chat, updateInfo);
            else
                taskStarter = () => OnOtherEvents(updateInfo);

            taskInfo.Task = Task.Run(taskStarter).ContinueWith(async t =>
            {
                lock (taskInfo)
                    if (taskInfo.Semaphore.CurrentCount == 0)
                    {
                        taskInfo.Task = null;
                        return;
                    }
                var newUpdate = await ((IGetNext)updateInfo).NextUpdate(_cancel.Token);
                RunTask(taskInfo, newUpdate, chat);
            });
        }

        public async Task<UpdateKind> NextEvent(UpdateInfo update, CancellationToken ct = default)
        {
            using (var bothCT = CancellationTokenSource.CreateLinkedTokenSource(ct, _cancel.Token))
            {
                var newUpdate = await ((IGetNext)update).NextUpdate(bothCT.Token);
                update.Message = newUpdate.Message;
                update.CallbackData = newUpdate.CallbackData;
                update.Update = newUpdate.Update;
                return update.UpdateKind = newUpdate.UpdateKind;
            }
        }

        public async Task<string> ButtonClicked(UpdateInfo update, Message msg = null, CancellationToken ct = default)
        {
            while (true)
            {
                switch (await NextEvent(update, ct))
                {
                    case UpdateKind.CallbackQuery:
                        if (msg != null && update.Message.MessageId != msg.MessageId)
                            _ = Telegram.AnswerCallbackQueryAsync(update.Update.CallbackQuery.Id, null, cancellationToken: ct);
                        else
                            return update.CallbackData;
                        continue;
                    case UpdateKind.OtherUpdate:
                        if (update.Update.MyChatMember is ChatMemberUpdated chatMemberUpdated)
                        {
                            if (chatMemberUpdated.NewChatMember.Status == ChatMemberStatus.Left || chatMemberUpdated.NewChatMember.Status == ChatMemberStatus.Kicked)
                                throw new LeftTheChatException(); // abort the calling method
                        }
                        break;
                }
            }
        }

        public async Task<MsgCategory> NewMessage(UpdateInfo update, CancellationToken ct = default)
        {
            while (true)
            {
                switch (await NextEvent(update, ct))
                {
                    case UpdateKind.NewMessage:
                        if (update.MsgCategory == MsgCategory.Text || update.MsgCategory == MsgCategory.MediaOrDoc || update.MsgCategory == MsgCategory.StickerOrDice)
                            return update.MsgCategory; // NewMessage only returns for messages from these 3 categories
                        break;
                    case UpdateKind.CallbackQuery:
                        _ = Telegram.AnswerCallbackQueryAsync(update.Update.CallbackQuery.Id, null, cancellationToken: ct);
                        continue;
                    case UpdateKind.OtherUpdate:
                        if (update.Update.MyChatMember is ChatMemberUpdated chatMemberUpdated)
                        {
                            if (chatMemberUpdated.NewChatMember.Status == ChatMemberStatus.Left || chatMemberUpdated.NewChatMember.Status == ChatMemberStatus.Kicked)
                                throw new LeftTheChatException(); // abort the calling method
                        }
                        break;
                }
            }
        }

        public async Task<string> NewTextMessage(UpdateInfo update, CancellationToken ct = default)
		{
			while (await NewMessage(update, ct) != MsgCategory.Text) { }
			return update.Message.Text;
		}

		public void ReplyCallback(UpdateInfo update, string text = null, bool showAlert = false, string url = null)
		{
			if (update.Update.Type != UpdateType.CallbackQuery)
				throw new InvalidOperationException("This method can be called only for CallbackQuery updates");
			_ = Telegram.AnswerCallbackQueryAsync(update.Update.CallbackQuery.Id, text, showAlert, url);
		}
	}

	public class LeftTheChatException : Exception
	{
		public LeftTheChatException() : base("The chat was left") { }
	}
}
