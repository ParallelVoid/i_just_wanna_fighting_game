namespace FightingGame.CharacterSelect
{
    /// <summary>One player's selection, independent of UI and the other player's cursor.</summary>
    public sealed class PlayerSelectionState
    {
        public int CharacterIndex { get; private set; }
        public int VariationIndex { get; private set; }
        public bool ChoosingVariation { get; private set; }
        public bool IsReady { get; private set; }

        public void Preview(int index)
        {
            if (ChoosingVariation || IsReady) return;
            CharacterIndex = index;
            VariationIndex = 0;
        }
        public void LockCharacter(int index)
        {
            if (ChoosingVariation || IsReady) return;
            CharacterIndex = index;
            VariationIndex = 0;
            ChoosingVariation = true;
        }
        public void ChangeVariation(int delta, int count)
        {
            if (!ChoosingVariation || IsReady || count <= 0) return;
            VariationIndex = ((VariationIndex + delta) % count + count) % count;
        }
        public bool Confirm()
        {
            if (!ChoosingVariation || IsReady) return false;
            IsReady = true;
            return true;
        }
        public void Back()
        {
            IsReady = false;
            ChoosingVariation = false;
            VariationIndex = 0;
        }
    }
}
