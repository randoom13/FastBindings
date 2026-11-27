namespace FastBindings.Interfaces
{
    public interface IPropertyAccessor
    {
        object? GetProperty(string propertyName);
        void SetProperty(string propertyName, object? value);
    }
}
