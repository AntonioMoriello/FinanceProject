namespace FinanceManager.Models.ViewModels
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public string? ErrorMessage { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
        public int? StatusCode { get; set; }
        public string? ErrorTitle { get; set; }

        // Additional properties for development environment
        public string? ExceptionMessage { get; set; }
        public string? ExceptionStackTrace { get; set; }
        public string? ExceptionType { get; set; }
        public bool ShowDetailedError { get; set; }

        // Properties for user-friendly error messages
        public string UserFriendlyMessage => ErrorMessage ?? "An error occurred while processing your request.";
        public string SupportMessage => "If this problem persists, please contact support.";
        public string ReturnUrl { get; set; } = "/";

        // Common error messages
        public static class CommonErrors
        {
            public const string NotFound = "The requested resource was not found.";
            public const string Unauthorized = "You are not authorized to access this resource.";
            public const string BadRequest = "The request was invalid or cannot be processed.";
            public const string ServerError = "An internal server error occurred.";
            public const string DatabaseError = "A database error occurred.";
            public const string ValidationError = "One or more validation errors occurred.";
            public const string ConcurrencyError = "The data was modified by another user.";
        }

        // Helper method to create common error instances
        public static ErrorViewModel CreateFrom(Exception exception, string? requestId = null, bool isDevelopment = false)
        {
            return new ErrorViewModel
            {
                RequestId = requestId,
                ErrorMessage = isDevelopment ? exception.Message : "An error occurred while processing your request.",
                ExceptionMessage = exception.Message,
                ExceptionStackTrace = exception.StackTrace,
                ExceptionType = exception.GetType().Name,
                ShowDetailedError = isDevelopment
            };
        }

        public static ErrorViewModel NotFoundError(string message = null)
        {
            return new ErrorViewModel
            {
                StatusCode = 404,
                ErrorTitle = "Not Found",
                ErrorMessage = message ?? CommonErrors.NotFound
            };
        }

        public static ErrorViewModel UnauthorizedError(string message = null)
        {
            return new ErrorViewModel
            {
                StatusCode = 401,
                ErrorTitle = "Unauthorized",
                ErrorMessage = message ?? CommonErrors.Unauthorized
            };
        }

        public static ErrorViewModel BadRequestError(string message = null)
        {
            return new ErrorViewModel
            {
                StatusCode = 400,
                ErrorTitle = "Bad Request",
                ErrorMessage = message ?? CommonErrors.BadRequest
            };
        }

        public static ErrorViewModel ServerError(string message = null)
        {
            return new ErrorViewModel
            {
                StatusCode = 500,
                ErrorTitle = "Server Error",
                ErrorMessage = message ?? CommonErrors.ServerError
            };
        }
    }
}