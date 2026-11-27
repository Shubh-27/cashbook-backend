namespace backend.common
{
    public sealed class StatementParsingException : Exception
    {
        public string Code { get; }

        public StatementParsingException(string code, string message) : base(message)
        {
            Code = code;
        }
    }
}
