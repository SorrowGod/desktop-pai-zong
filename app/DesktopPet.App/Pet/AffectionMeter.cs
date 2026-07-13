namespace DesktopPet.App.Pet;

public sealed class AffectionMeter
{
    public AffectionMeter(int initialValue) => Value = Math.Clamp(initialValue, 0, 100);

    public int Value { get; private set; }

    public int Add(int amount)
    {
        Value = Math.Clamp(Value + amount, 0, 100);
        return Value;
    }

    public int Remove(int amount) => Add(-Math.Abs(amount));
}
