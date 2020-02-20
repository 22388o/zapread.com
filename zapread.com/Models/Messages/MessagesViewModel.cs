﻿using System;
using System.Collections.Generic;
using zapread.com.Models.Database;

namespace zapread.com.Models
{
    public class MessagesViewModel
    {
        public List<UserMessage> Messages = new List<UserMessage>();
        public List<UserAlert> Alerts = new List<UserAlert>();
    }

    public class UnreadModel
    {
        public int NumUnread = 0;
    }

    public class RecentUnreadMessagesViewModel
    {
        public List<UserMessage> Messages = new List<UserMessage>();
    }

    public class RecentUnreadAlertsViewModel
    {
        public List<UserAlert> Alerts = new List<UserAlert>();
    }

    public class ChatMessageViewModel
    {
        public UserMessage Message { get; set; }

        public DateTime TimeStamp { get; set; }
        public string Content { get; set; }
        public string FromName { get; set; }
        public string FromAppId { get; set; }
        public bool IsReceived { get; set; }
        public User From { get; set; }
        public User To { get; set; }
        public int FromProfileImgVersion { get; set; }
    }

    public class ChatMessagesViewModel
    {
        public User OtherUser { get; set; }
        public User ThisUser { get; set; }
        public List<ChatMessageViewModel> Messages = new List<ChatMessageViewModel>();
    }
}