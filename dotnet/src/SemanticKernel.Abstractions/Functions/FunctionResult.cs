﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

#pragma warning disable IDE0130
namespace Microsoft.SemanticKernel;
#pragma warning restore IDE0130

/// <summary>
/// Function result after execution.
/// </summary>
public sealed class FunctionResult
{
    internal Dictionary<string, object>? _metadata;

    /// <summary>
    /// Name of executed function.
    /// </summary>
    public string FunctionName { get; internal set; }

    /// <summary>
    /// Return true if the function result is for a function that was cancelled.
    /// </summary>
    public bool IsCancellationRequested { get; internal set; }

    /// <summary>
    /// Return true if the function should be skipped.
    /// </summary>
    public bool IsSkipRequested { get; internal set; }

    /// <summary>
    /// Return true if the function should be repeated.
    /// </summary>
    public bool IsRepeatRequested { get; internal set; }

    /// <summary>
    /// Metadata for storing additional information about function execution result.
    /// </summary>
    public Dictionary<string, object> Metadata
    {
        get => this._metadata ??= new();
        internal set => this._metadata = value;
    }

    /// <summary>
    /// Function result object.
    /// </summary>
    internal object? Value { get; private set; } = null;

    /// <summary>
    /// The culture configured on the Kernel that executed the function.
    /// </summary>
    internal CultureInfo Culture { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionResult"/> class.
    /// </summary>
    /// <param name="functionName">Name of executed function.</param>
    public FunctionResult(string functionName)
    {
        this.FunctionName = functionName;
        this.Culture = CultureInfo.InvariantCulture;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionResult"/> class.
    /// </summary>
    /// <param name="functionName">Name of executed function.</param>
    /// <param name="value">Function result object.</param>
    /// <param name="culture">The culture configured on the Kernel that executed the function.</param>
    public FunctionResult(string functionName, object? value, CultureInfo culture)
        : this(functionName)
    {
        this.Value = value;
        this.Culture = culture;
    }

    /// <summary>
    /// Returns function result value.
    /// </summary>
    /// <typeparam name="T">Target type for result value casting.</typeparam>
    /// <exception cref="InvalidCastException">Thrown when it's not possible to cast result value to <typeparamref name="T"/>.</exception>
    public T? GetValue<T>()
    {
        if (this.Value is null)
        {
            return default;
        }

        if (this.Value is T typedResult)
        {
            return typedResult;
        }

        throw new InvalidCastException($"Cannot cast {this.Value.GetType()} to {typeof(T)}");
    }

    /// <summary>
    /// Get typed value from metadata.
    /// </summary>
    public bool TryGetMetadataValue<T>(string key, out T value)
    {
        if (this._metadata is { } metadata &&
            metadata.TryGetValue(key, out object? valueObject) &&
            valueObject is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default!;
        return false;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return ConvertToString(this.Value, this.Culture) ?? string.Empty;
    }

    private static string? ConvertToString(object? value, CultureInfo culture)
    {
        if (value == null) { return null; }

        var sourceType = value.GetType();

        var converterFunction = GetTypeConverterDelegate(sourceType);

        return converterFunction == null
            ? value.ToString()
            : converterFunction(value, culture);
    }

    private static Func<object?, CultureInfo, string?>? GetTypeConverterDelegate(Type sourceType) =>
        s_converters.GetOrAdd(sourceType, static sourceType =>
        {
            // Strings just render as themselves.
            if (sourceType == typeof(string))
            {
                return (input, cultureInfo) => (string)input!;
            }

            // Look up and use a type converter.
            if (GetTypeConverter(sourceType) is TypeConverter converter && converter.CanConvertTo(typeof(string)))
            {
                return (input, cultureInfo) =>
                {
                    return converter.ConvertToString(context: null, cultureInfo, input);
                };
            }

            return null;
        });

    private static TypeConverter? GetTypeConverter(Type sourceType)
    {
        if (sourceType == typeof(byte)) { return new ByteConverter(); }
        if (sourceType == typeof(sbyte)) { return new SByteConverter(); }
        if (sourceType == typeof(bool)) { return new BooleanConverter(); }
        if (sourceType == typeof(ushort)) { return new UInt16Converter(); }
        if (sourceType == typeof(short)) { return new Int16Converter(); }
        if (sourceType == typeof(char)) { return new CharConverter(); }
        if (sourceType == typeof(uint)) { return new UInt32Converter(); }
        if (sourceType == typeof(int)) { return new Int32Converter(); }
        if (sourceType == typeof(ulong)) { return new UInt64Converter(); }
        if (sourceType == typeof(long)) { return new Int64Converter(); }
        if (sourceType == typeof(float)) { return new SingleConverter(); }
        if (sourceType == typeof(double)) { return new DoubleConverter(); }
        if (sourceType == typeof(decimal)) { return new DecimalConverter(); }
        if (sourceType == typeof(TimeSpan)) { return new TimeSpanConverter(); }
        if (sourceType == typeof(DateTime)) { return new DateTimeConverter(); }
        if (sourceType == typeof(DateTimeOffset)) { return new DateTimeOffsetConverter(); }
        if (sourceType == typeof(Uri)) { return new UriTypeConverter(); }
        if (sourceType == typeof(Guid)) { return new GuidConverter(); }

        if (sourceType.GetCustomAttribute<TypeConverterAttribute>() is TypeConverterAttribute tca &&
            Type.GetType(tca.ConverterTypeName, throwOnError: false) is Type converterType &&
            Activator.CreateInstance(converterType) is TypeConverter converter)
        {
            return converter;
        }

        return null;
    }

    /// <summary>Converter functions for converting types to strings.</summary>
    private static readonly ConcurrentDictionary<Type, Func<object?, CultureInfo, string?>?> s_converters = new();
}
