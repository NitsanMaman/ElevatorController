using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElevatorsController.Models
{
    // This class represents a Building
    public class Building
    {
        // A list to store all the elevators in the building
        public List<Elevator> Elevators { get; set; }

        // A property to store the total number of floors in the building
        public int TotalFloors { get; set; } = 11;

        public Building(int numberOfElevators)
        {
            // Initialize the list of elevators
            Elevators = new List<Elevator>();
            for (int i = 0; i < numberOfElevators; i++)
            {
                Elevators.Add(new Elevator());
            }
        }
    }

}
