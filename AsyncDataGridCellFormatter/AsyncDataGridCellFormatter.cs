public class AsyncFormatter : DependencyObject
{
    public class CacheEntry : INotifyPropertyChanged
    {
        public object Data { get; set; }

        public bool IsFormatted { get; set; }

        #region observable properties

        private string? _UnformattedValue;
        public string? UnformattedValue
        {
            get => _UnformattedValue;
            set
            {
                _UnformattedValue = value;
                OnPropertyChanged("UnformattedValue");
            }
        }

        private string? _FormattedValue;
        public string? FormattedValue
        {
            get => _FormattedValue;
            set
            {
                _FormattedValue = value;
                OnPropertyChanged("FormattedValue");
            }
        }
        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        #endregion
    }

    #region dependency properties

    //
    // This DP is linked to a given cell's TextBlock.Text property, so whenever the
    // value is populated, we kick off formatting.
    //
    public static readonly DependencyProperty ObservedTextProperty =
        DependencyProperty.RegisterAttached(
            "ObservedText",
            typeof(string),
            typeof(AsyncFormatter),
            new PropertyMetadata(default(string), OnObservedTextChanged));

    public static string GetObservedText(DependencyObject obj)
    {
        return (string)obj.GetValue(ObservedTextProperty);
    }

    public static void SetObservedText(DependencyObject obj, string value)
    {
        obj.SetValue(ObservedTextProperty, value);
    }

    #endregion

    private ConcurrentBag<CacheEntry> m_Cache;

    public AsyncFormatter()
    {
        m_Cache = new ConcurrentBag<CacheEntry>();
    }

    public CacheEntry GetFormatterEntry(string Contents)
    {
        var cached = m_Cache.FirstOrDefault(c => c.UnformattedValue == Contents);
        if (cached != default)
        {
            return cached;
        }
        //
        // The caller should populate...
        //
        return new CacheEntry();
    }

    private static async void OnObservedTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var textBlock = d as TextBlock;
        if (textBlock == null)
        {
            Debug.Assert(false);
            return;
        }

        //
        // Retrieve the column formatter from the TextBlock's Tag.
        //
        var asyncFormatter = textBlock.Tag as AsyncFormatter;
        if (asyncFormatter == null)
        {
            Debug.Assert(false);
            return;
        }

        //
        // Get the cell's current contents.
        //
        var contents = e.NewValue as string;
        if (string.IsNullOrEmpty(contents))
        {
            return;
        }

        //
        // See if this value has been formatted before. If so, stop processing now.
        //
        var context = asyncFormatter.GetFormatterEntry(contents);
        if (context.IsFormatted)
        {
            //
            // Remove the binding for this cell, it is done
            //
            BindingOperations.ClearBinding(textBlock, TextBlock.TextProperty);
            textBlock.Text = context.FormattedValue;
            return;
        }

        context.Data = new object(); // actual data to share with the formatter here
        context.UnformattedValue = contents;

        //
        // Overwrite this cell's binding to the result of the async formatter.
        //
        textBlock.SetBinding(TextBlock.TextProperty, new Binding()
        {
            Source = context,
            Path = new PropertyPath("FormattedValue"),
            IsAsync = true
        });
        await asyncFormatter.RunAsync(context);
        context.IsFormatted = true;
    }

    private async Task RunAsync(CacheEntry Entry)
    {
        Debug.Assert(Entry.FormattedValue == null);
        Debug.Assert(Entry.Data != null);

        //
        // Do async formatter work here
        //
    }
}