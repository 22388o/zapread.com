﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace zapread.com.Models.API.Groups
{
    public class CheckExistsGroupResponse : ZapReadResponse
    {
        public bool exists { get; set; }
    }
}