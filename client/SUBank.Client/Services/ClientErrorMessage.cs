namespace SUBank.Client.Services;

public static class ClientErrorMessage
{
    public static string From(Exception exception) => exception switch
    {
        ApiRequestException apiException => apiException.Message,
        LogoutNotConfirmedException logoutException => logoutException.Message,
        HttpRequestException =>
            "Không thể kết nối đến dịch vụ SUBank. Vui lòng kiểm tra kết nối và thử lại.",
        TaskCanceledException => "Dịch vụ phản hồi quá chậm. Vui lòng thử lại.",
        InvalidOperationException invalidOperationException => invalidOperationException.Message,
        _ => "Không thể hoàn tất thao tác lúc này. Vui lòng thử lại."
    };
}
