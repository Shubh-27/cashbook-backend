using System;
using System.Collections.Generic;
using System.Text;

namespace backend.common
{
    public class Constants
    {
        public class StatusType
        {
            public const string ActiveString = "Active";
            public const byte Active = 1;
            public const string InactiveString = "In Active";
            public const byte Inactive = 7;
            public const string DeleteString = "Deleted";
            public const byte Delete = 3;
        }
        public static class UserType
        {
            public const string AdministratorString = "Administrator";
            public const byte Administrator = 0;
            public const string AdviserString = "Adviser";
            public const byte Adviser = 2;
        }
    }
}
