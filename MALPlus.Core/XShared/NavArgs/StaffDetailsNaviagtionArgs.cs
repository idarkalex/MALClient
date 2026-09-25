namespace MALClient.XShared.NavArgs
{
    public class StaffDetailsNaviagtionArgs
    {
        public int Id { get; set; }

        public bool ResetNav { get; set; }

        public override bool Equals(object obj)
        {
            var arg = obj as StaffDetailsNaviagtionArgs;
            return arg?.Id == Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}
