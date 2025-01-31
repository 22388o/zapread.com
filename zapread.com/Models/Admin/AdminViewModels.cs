﻿using System;

namespace zapread.com.Models
{
    public class AuditUserViewModel
    {
        public string Username { get; set; }
    }

    public class Stat
    {
        public Int64 TimeStampUtc { get; set; }
        public int Count { get; set; }
    }

    public enum DateGroupType
    {
        Day,
        Week,
        Month,
        Quarter,
        Year
    }
}