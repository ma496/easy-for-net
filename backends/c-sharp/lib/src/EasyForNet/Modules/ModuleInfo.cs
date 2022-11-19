namespace EasyForNet.Modules
{
    public class ModuleInfo
    {
        public int Level { get; }
        public ModuleBase Module { get; }

        public ModuleInfo(int level, ModuleBase module)
        {
            Level = level;
            Module = module;
        }
    }
}