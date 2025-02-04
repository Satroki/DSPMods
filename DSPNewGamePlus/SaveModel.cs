using System;
using System.Collections.Generic;
using System.Text;

namespace DSPNewGamePlus
{
    public class SaveModel
    {
        public History History { get; set; }
        public Mecha Mecha { get; set; }
    }

    public class History
    {
        public int[] recipeUnlocked;
        public int[] tutorialUnlocked;
        public Dictionary<int, TechState> techDict;
        public long universeMatrixPointUploaded;
    }

    public class Mecha
    {

    }
}
