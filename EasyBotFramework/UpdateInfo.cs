using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace YourEasyBot
{
	public enum UpdateKind { None, NewMessage, EditedMessage, CallbackQuery, OtherUpdate }
	public enum MsgCategory { Other, Text, MediaOrDoc, StickerOrDice, Sharing, ChatStatus, VideoChat }

	public class UpdateInfo : IGetNext
	{
		public UpdateKind UpdateKind;
		public Message Message;
		public string CallbackData;
		public Update Update;

        public MsgCategory MsgCategory
        {
            get
            {
                if (Message?.Type == MessageType.Text)
                    return MsgCategory.Text;
                else if (Message?.Type == MessageType.Photo || Message?.Type == MessageType.Audio || Message?.Type == MessageType.Video || Message?.Type == MessageType.Voice || Message?.Type == MessageType.Document || Message?.Type == MessageType.VideoNote)
                    return MsgCategory.MediaOrDoc;
                else if (Message?.Type == MessageType.Sticker || Message?.Type == MessageType.Dice)
                    return MsgCategory.StickerOrDice;
                else if (Message?.Type == MessageType.Location || Message?.Type == MessageType.Contact || Message?.Type == MessageType.Venue || Message?.Type == MessageType.Game || Message?.Type == MessageType.Invoice || Message?.Type == MessageType.SuccessfulPayment || Message?.Type == MessageType.WebsiteConnected)
                    return MsgCategory.Sharing;
                else if (Message?.Type == MessageType.ChatMembersAdded || Message?.Type == MessageType.ChatMemberLeft || Message?.Type == MessageType.ChatTitleChanged || Message?.Type == MessageType.ChatPhotoChanged || Message?.Type == MessageType.MessagePinned || Message?.Type == MessageType.ChatPhotoDeleted || Message?.Type == MessageType.GroupCreated || Message?.Type == MessageType.SupergroupCreated || Message?.Type == MessageType.ChannelCreated || Message?.Type == MessageType.MigratedToSupergroup || Message?.Type == MessageType.MigratedFromGroup)
                    return MsgCategory.ChatStatus;
                else if (Message?.Type == MessageType.VideoChatScheduled || Message?.Type == MessageType.VideoChatStarted || Message?.Type == MessageType.VideoChatEnded || Message?.Type == MessageType.VideoChatParticipantsInvited)
                    return MsgCategory.VideoChat;
                else
                    return MsgCategory.Other;
            }
        }


        private readonly TaskInfo _taskInfo;
		internal UpdateInfo(TaskInfo taskInfo) => _taskInfo = taskInfo;
		async Task<UpdateInfo> IGetNext.NextUpdate(CancellationToken cancel)
		{
			await _taskInfo.Semaphore.WaitAsync(cancel);
			UpdateInfo newUpdate;
			lock (_taskInfo)
				newUpdate = _taskInfo.Updates.Dequeue();
			return newUpdate;
		}
	}
}
