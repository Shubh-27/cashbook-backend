namespace backend.common
{
    public class Constants
    {
        public const string CorsPolicyName = "AllowAll";
        public const string InitialMigrationId = "20260315212014_InitialCreate";
        public const int StatementSampleTopRows = 70;
        public const int StatementSampleBottomRows = 30;

        public static class SqliteErrorCodes
        {
            public const int ConstraintViolation = 19;
        }

        public static class ImportRowStatus
        {
            public const string New = "new";
            public const string Invalid = "invalid";
            public const string ExactDuplicate = "exact_duplicate";
            public const string PossibleDuplicate = "possible_duplicate";
        }

        public class StatusType
        {
            public const string ActiveString = "Active";
            public const byte Active = 1;
            public const string InactiveString = "In Active";
            public const byte Inactive = 7;
            public const string DeleteString = "Deleted";
            public const byte Delete = 3;
        }
    }
}
