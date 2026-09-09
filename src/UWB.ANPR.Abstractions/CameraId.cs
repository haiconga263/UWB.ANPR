namespace UWB.ANPR.Abstractions;

/// <summary>Định danh duy nhất của một camera trong hệ thống.</summary>
public readonly record struct CameraId
{
    private CameraId(string value) => Value = value;

    public string Value { get; }

    public static CameraId From(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new CameraId(value.Trim());
    }

    public override string ToString() => Value;
}
