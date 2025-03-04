# AsyncDataGridCellFormatter
This sample shows how to asynchronously format the contents of a `DataGridCell`. This might be desirable if formatting can take a long time and you don't want to block the UI thread. If the formatting is quick, `IConverter` is the correct approach.

# Example Usage

Let's assume you have a custom `UserControl` that contains a `DataGrid` which needs this capability. Let's also assume the columns are dynamically generated and of an unknown number.

```
<UserControl x:Class="MyApp.Controls.MyCustomDataGrid"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" 
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             DataContext="{Binding MyDc,Path=MyObservableCollection}">
    <ScrollViewer VerticalScrollBarVisibility ="Auto"
                  HorizontalScrollBarVisibility = "Auto"
                  HorizontalAlignment = "Stretch"
                  VerticalAlignment = "Stretch"
                  CanContentScroll = "True">
        <DataGrid Name = "MyCustomDataGrid"
                  AlternatingRowBackground="AliceBlue"
                  EnableColumnVirtualization = "True"
                  EnableRowVirtualization ="True"
                  IsReadOnly = "True"
                  AutoGenerateColumns = "True"
                  RowHeight = "25"
                  MaxHeight = "1600"
                  MaxWidth = "1600"
                  HorizontalAlignment ="Stretch"
                  ItemsSource="{Binding Data.AsObservable}"
                  DataContext="{Binding .}"
                  AutoGeneratingColumn="MyCustomDataGrid_AutoGeneratingColumn"/>
    </ScrollViewer>
</UserControl>
```

In the code-behind, we'll implement our column creation using `AutoGeneratingColumn` technique and setup the proper bindings to know when to start async formatting:

```
private void MyCustomDataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
{
    var textColumn = e.Column as DataGridTextColumn;
    if (textColumn == null)
    {
        Debug.Assert(false);
        return;
    }

    //
    // Create a new DataGridTemplateColumn to replace the DataGridTextColumn.
    // This provides us more flexibility in manipulating the cell's TextBlock.
    //
    var templateColumn = new DataGridTemplateColumn()
    {
        Header = matchedColumn.Name,
    };

    //
    // Create an async formatter for this column. All cells in this column will use
    // this one formatter.
    //
    var asyncFormatter = new AsyncFormatter();

    //
    // Create a template for all cells in this column.
    // The template will leverage the existing binding in the DataGridTextColumn
    // and we'll also reuse that binding for our own static dependency property,
    // which allows us to be notified when the cell's content is populated so we
    // can begin async formatting.
    //
    var cellTemplate = new DataTemplate();
    var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
    textBlockFactory.SetValue(TextBlock.TagProperty, asyncFormatter);
    textBlockFactory.SetBinding(TextBlock.TextProperty, textColumn.Binding);
    textBlockFactory.SetBinding(AsyncFormatter.ObservedTextProperty, textColumn.Binding);
    cellTemplate.VisualTree = textBlockFactory;
    templateColumn.CellTemplate = cellTemplate;
    e.Column = templateColumn;
}
```

The process works as follows:

1. An object is added to the `DataGrid` via the list binding.
1. As a row is populated, a cell is created. Its `Text` property is bound to the appropriate property of the item that the row is bound to. This value is populated in the cell's `TextBlock` UI control.
1. As part of our cell template, the `ObservedTextProperty` dependency property is also notified of the new contents. We registered the callback `OnObservedTextChanged` to be invoked whenever this happens.
1. Inside `OnObservedTextChanged`, we check our cache of formatted values.
   * If this value has been previously formatted, we erase this particular cell's binding to avoid recursion and trying to format a previously formatted value. We then assign the already-formatted value to this cell.
   * If this value has never been formatted, we initialize a new cache entry to hold the formatted result, overwrite the cell's `Text` property with a new binding on the async result, and launch an async `Task` to carry out the formatting operation. Because the binding refers to an `INotifyPropertyChanged` property, this update is automatic. When the `async` operation completes, we mark the cache entry as formatted. 

# Further reading

https://learn.microsoft.com/en-us/archive/msdn-magazine/2014/march/async-programming-patterns-for-asynchronous-mvvm-applications-data-binding