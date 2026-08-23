namespace IcdModelsLIbrary
{
    public interface IItem
    {
        int Id { get; }
        int Location { get; }
        string Name { get; }
        string Mask { get; }
        int Size { get; }
        int Min { get; }
        int Max { get; }
        DataType Type { get; }
    }
}
