namespace Creobit.Bootstrap.Core.Scripts.Runtime.Operation
{
    public struct OperationResult
    {
        public bool IsSuccess { get; private set; }
        public string ErrorMessage { get; private set; }

        public OperationResult(bool isSuccess, string errorMessage)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
        }
    }
}