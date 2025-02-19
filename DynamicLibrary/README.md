# DynamicLibrary
This sample uses the C-Sharp Compiler (CSC) to dynamically generate a binary that contains classes that can be instantiated by another application. `DynamicLibrary`, as the name suggests, could be useful in a scenario where a user supplies code they want to run and the application needs to instantiate and run it at some critical point.

# Example Usage

As an example, let's assume you want to allow the user of your application to define `IConverter` code snippets to format a column in a WPF `DataGrid` to their liking. A `DynamicLibrary` would contain the compiled code from their snippets and it would be invoked whenever a column was about to be populated with a value from the bound dataset.

## Create the library
The library needs to behave like an `IConverter`, so we need to instantiate the `DynamicLibrary` as follows:
* Specify any `using` statements necessary for the snippets to compile correctly
* Specify a namespace for the compiled code to live
* Specify the class name that will contain the `IConverter` code and an inheritence, in this case, `IConverter`
* Provide the class body, which is the user's code snippet

```
var dynamicLibrary = new DynamicLibrary(
    "using System; using System.Windows.Data;",
    "MyApplication.Utilities.Converters",
    "CustomConverter",
    "IValueConverter",
    userCodeSnippet
);
```

## Validate the user's code snippet

```
var result = dynamicLibrary.TryCompile(out string err);
if (!result)
{
    Console.WriteLine($"Compilation failed: {err}");
}
```

## Associate the `IConverter` to a `DataGrid`

As the premise is to auto-format `DataGrid` columns based on the user's `IConverter` code, we have to plug it into the `DataGrid`. Column specialization is typically done through the `AutoGeneratingColumn` event handler. In the code block below, the column of interest has a binding expression that invokes the `IConverter` we compiled into our `DynamicLibrary`, using the user's code snippet. In the case where the `IConverter` code should be reusable across multiple columns (for example, if the converter formats a decimal string to a hex string), we can tell the converter code which column is being formatted by passing the column name via the `ConverterParameter`. Inside the `IConverter` code, reflection can be used to retrieve the right value in the row's object to be formatted.

```
private void ProviderDataGrid_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
{
    var textColumn = e.Column as DataGridTextColumn;
    if (textColumn != null)
    {
        textColumn.Binding = new System.Windows.Data.Binding()
        {
            Path = new PropertyPath("."),
            Converter = dynamicLibrary.GetInstance() as IValueConverter;,
            ConverterParameter = e.Column.Header,
        };
    }
}
```

So, in this case, the `IConverter` code should expect to receive whatever `DataContext` is assigned to the `DataGrid` row being populated. If the `DataGrid` is bound to an `IList` of `Foo` objects, the convert would receive a `Foo` instance. If you instead just want to pass whatever field is being populated in the column, rather than the row object, replace the binding as follows:

```
textColumn.Binding = new System.Windows.Data.Binding()
{
    Path = new PropertyPath(e.Column.Header),
    Converter = dynamicLibrary.GetInstance() as IValueConverter;,
};
```

In this case, the converter will receive the value of the specific field in the object that corresponds to the column being populated (which is the normal pattern for `IConverter`).

## `IConverter` code

Let's assume the user wants to convert values destined for column `Bar` in row object type `FooObject` from a decimal value (string) as a hex value (string). In this case, the user would provide a string that contains the following code:

```
public class DecimalToHex : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        var foo = value as FooObject;
        if (foo != null)
        {
            var fieldName = parameter as string;
            if (fieldName == null)
            {
                Debug.Assert(false);
                return "";
            }
            var fieldvalue = GetPropertyValue(foo, fieldName);
            var numericvalue = System.Convert.ToInt64(fieldvalue);
            return $"0x{numericvalue:X}";
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
```

The reflection routine:

```
public static dynamic? GetPropertyValue(object Object, string PropertyName)
{
    object? value = null;
    try
    {
        var properties = Object.GetType().GetProperties(
            BindingFlags.Public | BindingFlags.Instance);
        properties.ToList().ForEach(property =>
        {
            if (property.Name.ToLower() == PropertyName.ToLower())
            {
                value = property.GetValue(Object, null);
            }
        });
    }
    catch (Exception) { }

    return value;
}
```

# Caveats

* Some sort of cache should be used so that a new library isn't created for every single column