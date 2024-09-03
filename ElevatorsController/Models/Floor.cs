using System.ComponentModel;


// This class represents a Floor
// It implements INotifyPropertyChanged to notify about property changes
public class Floor : INotifyPropertyChanged
{
    // Private variables to store whether the floor is targeted or not
    private bool _isTargeted;
    private bool _isUpTargeted;
    private bool _isDownTargeted;

    // Property to store the floor number
    public int FloorNumber { get; set; }

    // Property to check if the floor is targeted
    public bool IsTargeted
    {
        get => _isTargeted;
        set
        {
            if (_isTargeted != value)
            {
                _isTargeted = value;
                OnPropertyChanged(nameof(IsTargeted));
            }
        }
    }

    // Property to check if the floor is targeted for going up
    public bool IsUpTargeted
    {
        get => _isUpTargeted;
        set
        {
            if (_isUpTargeted != value)
            {
                _isUpTargeted = value;
                OnPropertyChanged(nameof(IsUpTargeted));
            }
        }
    }

    // Property to check if the floor is targeted for going down
    public bool IsDownTargeted
    {
        get => _isDownTargeted;
        set
        {
            if (_isDownTargeted != value)
            {
                _isDownTargeted = value;
                OnPropertyChanged(nameof(IsDownTargeted));
            }
        }
    }

    // This event is used to notify when a property has changed
    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        // Check if the PropertyChanged event has any subscribers
        // If it does, invoke the event with the name of the property that changed
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

