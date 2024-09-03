using ElevatorsController;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;

// This class represents an Elevator
// It implements INotifyPropertyChanged to notify about property changes
public class Elevator : INotifyPropertyChanged
{
    private double _currentFloor;
    public double CurrentFloor
    {
        get => _currentFloor;
        set
        {
            if (_currentFloor != value)
            {
                _currentFloor = value;
                OnPropertyChanged(nameof(CurrentFloor));
                OnPropertyChanged(nameof(CurrentIntFloor)); // Update CurrentIntFloor when CurrentFloor changes
            }
        }
    }

    // This property returns the current floor rounded to the nearest integer
    public int CurrentIntFloor => (int)Math.Round(CurrentFloor);

    // Properties to check if the elevator is moving up/down or standing
    public bool IsMovingUp { get; set; }
    public bool IsMovingDown { get; set; }
    public bool IsStanding { get; set; } = true;

    // A list of target floors that the elevator needs to reach
    public ObservableCollection<int> TargetFloors { get; set; } = new ObservableCollection<int>();

    // A list of floor states to check if a floor is targeted
    public ObservableCollection<bool> FloorStates { get; set; } = new ObservableCollection<bool>();

    // A thread for the elevator operation
    private Thread _elevatorThread;
    // A flag to control the running state of the elevator thread
    private bool _running = true;

    public Elevator()
    {
        // Initialize FloorStates to false (not targeted)
        for (int i = 0; i <= 10; i++)
        {
            FloorStates.Add(false);
        }
        // Start the elevator thread
        _elevatorThread = new Thread(ElevatorThreadStart);
        _elevatorThread.Start();
    }

    // Method to stop the elevator thread
    public void Stop()
    {
        _running = false;
        _elevatorThread.Join();
    }

    // Method that operate the elevator
    private void ElevatorThreadStart()
    {
        // Loop while the elevator is running
        while (_running)
        {
            // Check if there are any target floors to move to
            if (TargetFloors.Count > 0)
            {
                // Get the next target floor and move to it
                int nextFloor = TargetFloors[0];
                MoveElevatorToFloor(nextFloor);
            }
            Thread.Sleep(100); // Slight pause between checks 
        }
    }

    // Method to move the elevator to a specified floor
    private void MoveElevatorToFloor(int targetFloor)
    {
        //Decide which direction of motion base on current floor and target floor
        IsStanding = false;
        IsMovingUp = CurrentFloor < targetFloor;
        IsMovingDown = CurrentFloor > targetFloor;

        // Parameters to perfurme smooth slider animation
        const double stepSize = 0.01; // Smaller step for smoother movement
        const int totalMoveDuration = 2000; // Total duration to move 1 floor in milliseconds (2 seconds)
        int steps = (int)Math.Ceiling(1 / stepSize); // Number of steps to move 1 floor
        int stepDelay = totalMoveDuration / steps; // Delay per step to match the desired speed

        // Loop until the elevator reaches the target floor
        while (true)
        {
            // Check if we've reached the target floor
            if (Math.Abs(CurrentFloor - targetFloor) <= stepSize)
            {
                CurrentFloor = targetFloor; // Snap to exact floor
                break; // Exit loop as we've reached the target
            }

            // Move the elevator up or down depending on the direction
            if (IsMovingUp)
            {
                CurrentFloor += stepSize;
            }
            else if (IsMovingDown)
            {
                CurrentFloor -= stepSize;
            }

            // Update the UI with the current floor
            // Ensure the update happens on the UI thread
            App.Current.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(CurrentFloor));
            });

            Thread.Sleep(stepDelay); // Adjust delay for smoother updates

            // Check for new requests in the current direction
            // accordingly to "the elevator completes the move in one direction before switching to the opposite direction."
            int currentIntFloor = (int)Math.Round(CurrentFloor);


            if (IsMovingUp)
            {
                // Check for any floors above the current one that are closer than the target floor
                var nextUpFloor = TargetFloors.Where(f => f > currentIntFloor).OrderBy(f => f).FirstOrDefault();

                if (nextUpFloor > 0 && nextUpFloor < targetFloor)
                {
                    targetFloor = nextUpFloor; // Update the target floor
                }
            }


            else if (IsMovingDown)
            {
                // Check for any floors below the current one that are closer than the target floor
                var nextDownFloor = TargetFloors.Where(f => f < currentIntFloor).OrderByDescending(f => f).FirstOrDefault();
                if (nextDownFloor > 0 && nextDownFloor > targetFloor)
                {
                    targetFloor = nextDownFloor; // Update the target floor
                }
            }
        }

        // Final UI update to ensure the elevator is correctly positioned
        App.Current.Dispatcher.Invoke(() =>
        {
            OnPropertyChanged(nameof(CurrentFloor));
        });

        // Stand on the floor for 2 seconds
        Thread.Sleep(2000);

        // Remove the floor from the target floors list
        TargetFloors.Remove(targetFloor);

        // Update floor state when elevator reaches the floor
        UpdateFloorState(targetFloor, false);

        // Notify ViewModel to update floor state after reaching the floor
        ElevatorReachedFloor?.Invoke(this, targetFloor);

        // Notify the ViewModel to remove any redundant requests from the queue
        App.Current.Dispatcher.Invoke(() =>
        {
            ElevatorViewModel.Instance.RemoveRedundantRequests(targetFloor);
        });

        /*IsStanding = true;
        IsMovingUp = false;
        IsMovingDown = false;*/

        // Check if there are remaining floors to service in the current direction
        ServiceRemainingFloors();

        IsStanding = true; // Set the elevator to standing
    }

    // Method to service any remaining floors
    private void ServiceRemainingFloors()
    {
        int currentFloorInt = (int)Math.Round(CurrentFloor);

        if (IsMovingUp)
        {
            // Continue moving up if there are any floors above the current one
            var nextUpFloor = TargetFloors.Where(f => f > currentFloorInt).OrderBy(f => f).FirstOrDefault();
            if (nextUpFloor > 0)
            {
                MoveElevatorToFloor(nextUpFloor);
            }
            else
            {
                // If no more floors above, check for floors below and start moving down
                var nextDownFloor = TargetFloors.Where(f => f < currentFloorInt).OrderByDescending(f => f).FirstOrDefault();
                if (nextDownFloor > 0)
                {
                    IsMovingUp = false;
                    IsMovingDown = true;
                    MoveElevatorToFloor(nextDownFloor);
                }
                else
                {
                    // No more tasks, mark elevator as standing and check for new assignments
                    IsStanding = true;
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        ElevatorViewModel.Instance.CheckQueueForAvailableElevators();
                    });
                }
            }
        }
        else if (IsMovingDown)
        {
            // Continue moving down if there are any floors below the current one 
            int? nextDownFloor = TargetFloors.Where(f => f < currentFloorInt).OrderByDescending(f => f).Cast<int?>().FirstOrDefault(); // used cast here (fixing the default 0 FirstOrDefault() function was given to var making me lose 0 floor)
            if (nextDownFloor.HasValue)
            {
                MoveElevatorToFloor(nextDownFloor.Value);
            }
            else
            {
                // If no more floors below, check for floors above and start moving up
                var nextUpFloor = TargetFloors.Where(f => f > currentFloorInt).OrderBy(f => f).FirstOrDefault();
                if (nextUpFloor > 0)
                {
                    IsMovingUp = true;
                    IsMovingDown = false;
                    MoveElevatorToFloor(nextUpFloor);
                }
                else
                {
                    // No more tasks, mark elevator as standing and check for new assignments
                    IsStanding = true;
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        ElevatorViewModel.Instance.CheckQueueForAvailableElevators();
                    });
                }
            }
        }
    }


    // Event to notify the ViewModel when an elevator reaches a floor
    public event Action<Elevator, int> ElevatorReachedFloor;

    // Method to update the state of a specific floor and notify the change
    public void UpdateFloorState(int floor, bool state)
    {
        // Check if the floor number is within a valid range
        if (floor >= 0 && floor < FloorStates.Count)
        {
            FloorStates[floor] = state; 
            OnPropertyChanged(nameof(FloorStates)); 
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
