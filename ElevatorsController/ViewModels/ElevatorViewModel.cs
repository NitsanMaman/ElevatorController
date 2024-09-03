using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using ElevatorsController.Models;
using ElevatorsController;
using System.Windows;

// This class represents the ViewModel for the Elevator system
// It implements the INotifyPropertyChanged interface to notify the UI of property changes
public class ElevatorViewModel : INotifyPropertyChanged
{
    // Singleton instance of the ElevatorViewModel
    private static ElevatorViewModel _instance;
    public static ElevatorViewModel Instance => _instance ??= new ElevatorViewModel();

    // Collection of Elevator objects
    public ObservableCollection<Elevator> Elevators { get; set; }

    // Collection of Floor objects
    public ObservableCollection<Floor> Floors { get; set; }

    // Command for calling an elevator
    public ICommand CallElevatorCommand { get; }

    // Array of commands for moving elevators
    public ICommand[] MoveElevatorCommands { get; }

    // Field to indicate whether the system is in single direction mode (main task)
    private bool _isSingleDirectionMode = true; // Default to true

    // Property to get or set single direction mode 
    public bool IsSingleDirectionMode
    {
        get => _isSingleDirectionMode;
        set
        {
            if (_isSingleDirectionMode != value)
            {
                _isSingleDirectionMode = value;
                OnPropertyChanged(nameof(IsSingleDirectionMode));
                OnPropertyChanged(nameof(IsTwoDirectionMode)); // Notify inverse property change
            }
        }
    }

    // Property to indicate if the system is in two direction mode (additional functionality I added all floors has up and down buttons)
    public bool IsTwoDirectionMode => !_isSingleDirectionMode;

    // Queue to manage elevator requests (for edge situations)
    private Queue<(int floor, char direction)> requestQueue = new Queue<(int floor, char direction)>();

    
    public ElevatorViewModel()
    {
        // Initialize the command to call an elevator
        CallElevatorCommand = new RelayCommand<string>(parameter => CallElevator(parameter));

        // Initialize the elevators with 3 elevators from the Building model
        Elevators = new ObservableCollection<Elevator>(new Building(3).Elevators);

        // Initialize the floors collection
        Floors = new ObservableCollection<Floor>();
        for (int i = 0; i <= 10; i++)
        {
            Floors.Add(new Floor { FloorNumber = i });
        }

        // Initialize commands for moving each elevator
        MoveElevatorCommands = new ICommand[Elevators.Count];
        for (int i = 0; i < Elevators.Count; i++)
        {
            int elevatorIndex = i; // Capture the loop variable
            MoveElevatorCommands[i] = new RelayCommand<int>(floor => MoveElevator(elevatorIndex, floor)); 
        }

        // Subscribe to the ElevatorReachedFloor event for each elevator
        foreach (var elevator in Elevators)
        {
            elevator.ElevatorReachedFloor += OnElevatorReachedFloor;
        }

        // Subscribe to the application exit event
        App.Current.Exit += OnApplicationExit;
    }

    // Event handler for when an elevator reaches a floor
    private void OnElevatorReachedFloor(Elevator elevator, int floor)
    {
        // Update floor state in the ViewModel
        App.Current.Dispatcher.Invoke(() =>
        {
            if (elevator.IsMovingUp)
            {
                // Elevator reached the floor moving up, clear the up target
                if (Floors[floor].IsUpTargeted)
                {
                    Floors[floor].IsUpTargeted = false;
                    // Remove the corresponding request from the queue if it exists
                    RemoveRequestFromQueue(floor, '^');
                }
                else //Reached and goes down or IsSingleDirectionMode
                {
                    Floors[floor].IsDownTargeted = false;
                    Floors[floor].IsTargeted = false;
                }
            }
            else if (elevator.IsMovingDown)
            {
                // Elevator reached the floor moving down, clear the down target
                if (Floors[floor].IsDownTargeted)
                {
                    Floors[floor].IsDownTargeted = false;
                    // Remove the corresponding request from the queue if it exists
                    RemoveRequestFromQueue(floor, 'v');
                }
                else //Reached and goes up or IsSingleDirectionMode
                {
                    Floors[floor].IsUpTargeted = false;
                    Floors[floor].IsTargeted = false;
                }
            }
            else //elevator was in same floor of call, clear all targets - unique situation
            {
                Floors[floor].IsTargeted = false;
                Floors[floor].IsUpTargeted = false;
                Floors[floor].IsDownTargeted = false;
                // Remove redundant requests for this floor
                RemoveRequestFromQueue(floor, '^');
                RemoveRequestFromQueue(floor, 'v');
            }
            RemoveRedundantRequests(floor); // Remove redundant requests after reaching the floor
        });
        // After removing requests, check the queue for any pending requests
        CheckQueueForAvailableElevators();
    }

    // Method to remove a specific request from the queue
    public void RemoveRequestFromQueue(int floor, char direction)
    {
        // Temporary queue for updating
        var updatedQueue = new Queue<(int floor, char direction)>();

        // Process each request in the queue
        while (requestQueue.Count > 0)
        {
            // Get the next request
            var request = requestQueue.Dequeue();
            if (!(request.floor == floor && request.direction == direction))
            {
                updatedQueue.Enqueue(request); // Only re-enqueue if it doesn't match the floor and direction
            }
        }

        requestQueue = updatedQueue; // Update the queue
    }

    // Method to remove redundant requests for a specific floor
    public void RemoveRedundantRequests(int floor)
    {
        // Filter out any requests for the given floor
        var remainingRequests = requestQueue.Where(r => r.floor != floor).ToList();
        // Update the queue with remaining requests
        requestQueue = new Queue<(int floor, char direction)>(remainingRequests);
    }

    // Method to check the queue and assign available elevators
    public void CheckQueueForAvailableElevators()
    {
        // Process each request in the queue
        while (requestQueue.Count > 0)
        {
            // Get the next request from the queue and Find the closest available elevator to it
            var (floor, direction) = requestQueue.Dequeue();
            var closestElevator = FindClosestElevator(floor, direction);

            // Check if an elevator is available and not already targeting the floor
            if (closestElevator != null && !closestElevator.TargetFloors.Contains(floor))
            {

                // Add the target floor to the elevator's target list and Update the elevator's floor state
                closestElevator.TargetFloors.Add(floor);
                closestElevator.UpdateFloorState(floor, true);

                // Set the target direction for the floor
                if (direction == '^')
                {
                    Floors[floor].IsUpTargeted = true;
                }
                else if (direction == 'v')
                {
                    Floors[floor].IsDownTargeted = true;
                }

                Debug.WriteLine($"Request for floor {floor}{direction} processed from queue.");
            }
            else
            {
                // Re-add the request to the queue if no elevator is available
                requestQueue.Enqueue((floor, direction));
                break; // Exit the loop to avoid busy waiting
            }
        }
    }

    // Method to call an elevator based on the given parameter
    private void CallElevator(string parameter)
    {
        int floor;
        char direction = ' '; // Default direction

        // Check the mode and parse the parameter accordingly
        if ((parameter == "0" || parameter == "10") && IsTwoDirectionMode)
        {
            if (!int.TryParse(parameter, out floor))
            {
                Debug.WriteLine("Invalid floor number.");
                return;
            }
        }
        else if (IsSingleDirectionMode)
        {
            if (!int.TryParse(parameter, out floor))
            {
                Debug.WriteLine("Invalid floor number in SingleDirection mode.");
                return;
            }
        }
        else
        {
            if (parameter.Length < 2)
            {
                Debug.WriteLine("Invalid parameter format in TwoDirection mode.");
                return;
            }

            if (!int.TryParse(parameter.Substring(0, parameter.Length - 1), out floor))
            {
                Debug.WriteLine("Invalid floor number in TwoDirection mode.");
                return;
            }

            direction = parameter.Last();
            if (direction != '^' && direction != 'v')
            {
                Debug.WriteLine("Invalid direction in TwoDirection mode.");
                return;
            }
        }

        // Find the closest elevator based on the mode
        Elevator closestElevator;
        if (IsSingleDirectionMode)
        {
            closestElevator = FindClosestElevator(floor);
        }
        else
        {
            closestElevator = FindClosestElevator(floor, direction);
        }

        // Check if an elevator is available and not already targeting the floor
        if (closestElevator != null && !closestElevator.TargetFloors.Contains(floor))
        {
            // Check if an elevator is available and not already targeting the floor
            closestElevator.TargetFloors.Add(floor);
            closestElevator.UpdateFloorState(floor, true);

            // Set the target direction for the floor
            if (direction == '^')
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    Floors[floor].IsUpTargeted = true;
                });
            }
            else if (direction == 'v')
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    Floors[floor].IsDownTargeted = true;
                });
            }
        }
        else
        {
            // No available elevator found, enqueue the request
            requestQueue.Enqueue((floor, direction));
            Debug.WriteLine($"Request for floor {floor}{direction} added to the queue.");
        }
    }

    // Method to find the closest elevator to a given floor
    /* "The closest elevator serves the call from the floor. 
            The closest elevator should be selected from the standing elevators 
            or elevators which are currently moving towards the calling floor." */
    private Elevator FindClosestElevator(int floor)
    {
        Elevator closestElevator = null;
        int smallestDistance = int.MaxValue;

        // Check each elevator to find the closest one
        foreach (var elevator in Elevators)
        {
            int distance = Math.Abs((int)elevator.CurrentFloor - floor);

            // Check if the elevator can service the request 
            if (elevator.IsStanding || (elevator.IsMovingUp && elevator.CurrentFloor < floor) || (elevator.IsMovingDown && elevator.CurrentFloor > floor))
            {
                if (distance < smallestDistance)
                {
                    smallestDistance = distance; // Update the smallest distance
                    closestElevator = elevator;  // Set the closest elevator
                }
            }
        }

        return closestElevator;
    }

    // Method to find the closest elevator based on floor and direction (*** Additional ***)
    private Elevator FindClosestElevator(int floor, char direction)
    {
        Elevator closestElevator = null;
        int smallestDistance = int.MaxValue;

        // Check each elevator to find the closest one
        foreach (var elevator in Elevators)
        {
            int distance = Math.Abs((int)elevator.CurrentFloor - floor);

            // Check if the elevator is standing or moving in the right direction
            if (elevator.IsStanding)
            {
                if (distance < smallestDistance)
                {
                    smallestDistance = distance;
                    closestElevator = elevator;
                }
            }
            else
            {
                // if the elevator is moving up and there is a up request floor above 
                if (direction == '^' && elevator.IsMovingUp && elevator.CurrentFloor <= floor && !elevator.TargetFloors.Contains(floor))
                    /*|| direction == '^' && elevator.IsMovingDown && elevator.CurrentFloor >= floor && !elevator.TargetFloors.Contains(floor))*/ //tried to make the elevator as a collector if alot want to go up
                {
                    if (distance < smallestDistance)
                    {
                        smallestDistance = distance;
                        closestElevator = elevator;
                    }
                }
                // if the elevator is moving down and there is a down request floor below
                else if (direction == 'v' && elevator.IsMovingDown && elevator.CurrentFloor >= floor && !elevator.TargetFloors.Contains(floor))
                /*|| direction == 'v' && elevator.IsMovingUp && elevator.CurrentFloor <= floor && !elevator.TargetFloors.Contains(floor))*/ //tried to make the elevator as a collector if alot want to go down
                {
                    if (distance < smallestDistance)
                    {
                        smallestDistance = distance;
                        closestElevator = elevator;
                    }
                }
                else
                {
                    // If the elevator is moving but in a conflicting direction, skip it
                    continue;
                }
            }
        }

        return closestElevator;
    }

    // Method to add a floor to the elevator's destination list if it doesn't already exist, which will indirectly cause it to move towards it
    private void MoveElevator(int elevatorIndex, int floor)
    {
        var elevator = Elevators[elevatorIndex]; // Get the elevator by index
        if (!elevator.TargetFloors.Contains(floor))
        {
            elevator.TargetFloors.Add(floor); // Add the floor to the elevator's target list if not already present
        }
    }

    // Event handler for application exit
    private void OnApplicationExit(object sender, ExitEventArgs e)
    {
        // Stop all elevators
        foreach (var elevator in Elevators)
        {
            elevator.Stop();
        }
    }

    // Event to notify when a property has changed
    public event PropertyChangedEventHandler PropertyChanged;
    
    // Method to raise the PropertyChanged event
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
