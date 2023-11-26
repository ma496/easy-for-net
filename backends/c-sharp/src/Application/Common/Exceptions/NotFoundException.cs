﻿namespace EasyForNet.Application.Common.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException()
        : base("The specified resource was not found.")
    {
    }

    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public NotFoundException(string name, object key)
        : base($"Entity \"{name}\" ({key}) was not found.")
    {
    }
}
