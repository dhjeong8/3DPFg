namespace _3DPFg
{
    public struct Location {
        double x, y, z;
        Location() {}
        Location(Location location)
        {
            x = location.x;
            y = location.y;
            z = location.z;
        }
    }
    public class Agent
    {
        private Action a_action;
        private Location loc;
        private State state;

        public Agent() 
        {
            state = new State();
        }
        public Agent(Agent a)
        {
            this.a_action = new Action(a.a_action);
            this.a_loc = new Location(a.a_loc);
        }
        public Agent(Action a, Location l)
        {
            this.a_action = new Action(a);
            this.a_loc = new Location(l);
        }
        public void updateAction(Action na)
        {
            a_action = na;
        }

        public void updateLoc(Location new_loc)
        {
            a_loc = new Location(new_loc);
        }

        public Location getLoc()
        {
            return loc;
        }

        public double getX() { return loc.x; }
        public double getY() { return loc.y; }
        public double getZ() { return loc.z; }
    }
}
