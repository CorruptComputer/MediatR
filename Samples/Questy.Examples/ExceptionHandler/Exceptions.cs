using System;

namespace Questy.Examples.ExceptionHandler;

public class ConnectionException : Exception { }

public class ForbiddenException : ConnectionException { }

public class ResourceNotFoundException : ConnectionException { }

public class ServerException : Exception { }