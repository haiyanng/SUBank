namespace SUBank.Application.Exceptions;

public class BusinessRuleException(string message) : Exception(message);
public sealed class NotFoundException(string message) : Exception(message);
public sealed class ConflictException(string message) : Exception(message);
public class AuthenticationException(string message) : Exception(message);
public sealed class DependencyUnavailableException(string message, Exception? innerException = null) : Exception(message, innerException);
