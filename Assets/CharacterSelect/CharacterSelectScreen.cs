using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FightingGame.CharacterSelect
{
    [Serializable]
    public class FighterOption
    {
        public string name;
        public string style;
        public Color color;
        public Texture2D portrait;
        public Texture2D card;
        public string[] variations = { "AGGRESSIVE", "DEFENSIVE" };
        public Texture2D[] variationCards;
    }

    /// <summary>Controls selection using the authored scene UI. Layout transforms are owned by the scene.</summary>
    public class CharacterSelectScreen : MonoBehaviour
    {
        public FighterOption[] fighters = Array.Empty<FighterOption>();
        private readonly PlayerSelectionState[] players = { new PlayerSelectionState(), new PlayerSelectionState() };
        public PlayerSelectionState PlayerOne => players[0];
        public PlayerSelectionState PlayerTwo => players[1];
        // Keep the original P1 API for callers already using it.
        public int SelectedCharacterIndex => PlayerOne.CharacterIndex;
        public int SelectedVariationIndex => PlayerOne.VariationIndex;
        public bool IsConfirmed => PlayerOne.IsReady;
        public bool BothPlayersReady => PlayerOne.IsReady && PlayerTwo.IsReady;
        public event Action<FighterOption, int> SelectionConfirmed;
        /// <summary>Player number (1 or 2), fighter, variation.</summary>
        public event Action<int, FighterOption, int> PlayerSelectionConfirmed;
        public event Action BothSelectionsConfirmed;
        [SerializeField] private Transform ui;
        [SerializeField] private Color playerOneColor = new Color(.38f,.94f,.82f);
        [SerializeField] private Color playerTwoColor = new Color(1f,.58f,.28f);
        private bool readyNotificationSent;
        private int SlotCount => fighters.Length + 1;
        private readonly FighterOption randomOption = new FighterOption { name = "RANDOM", style = "ANY FIGHTER", color = new Color(.38f,.94f,.82f) };
        private string CardPath(int player) => player == 0 ? "Card" : "Card P2";
        private string ConfirmPath(int player) => player == 0 ? "Confirm" : "Confirm P2";
        private string BackPath(int player) => player == 0 ? "Back" : "Back P2";

        private void Awake()
        {
            if (ui == null) ui = transform.Find("Select UI");
            if (fighters == null || fighters.Length == 0 || ui == null)
            { ReportMissingLayout("saved UI or roster"); return; }
            for (int player = 0; player < 2; player++)
            {
                string card = CardPath(player);
                foreach (string path in new[] { card + "/Art/Source PNG" })
                    if (ui.Find(path) == null) { ReportMissingLayout(path); return; }
            }
            for (int i = 0; i < SlotCount; i++)
                if (ui.Find("Roster/Tile " + i + "/Portrait/Source PNG") == null)
                { ReportMissingLayout("Roster/Tile " + i); return; }
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).GetComponent<EventSystem>();
            // Each player's keyboard is handled here; Unity's shared Submit must not also select P1.
            eventSystem.sendNavigationEvents = false;
            for (int i = 0; i < SlotCount; i++)
            {
                int index = i;
                var tile = ui.Find("Roster/Tile " + i);
                tile.GetComponent<Button>().onClick.AddListener(() => ChooseCharacter(0, index));
                var trigger = tile.GetComponent<EventTrigger>() ?? tile.gameObject.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => Preview(0, index));
                trigger.triggers.Add(enter);
            }
            for (int i = 0; i < 2; i++)
            {
                int player = i;
                BindOptionalButton(CardPath(i) + "/Previous", () => ChangeVariation(player, -1));
                BindOptionalButton(CardPath(i) + "/Next", () => ChangeVariation(player, 1));
                BindOptionalButton(ConfirmPath(i), () => Confirm(player));
                BindOptionalButton(BackPath(i), () => Back(player));
            }
            PlayerTwo.Preview(Mathf.Min(1, fighters.Length - 1));
            Refresh();
        }

        private void Update()
        {
            HandleKeyboard(0, KeyCode.A, KeyCode.D, KeyCode.W, KeyCode.S, KeyCode.Space, KeyCode.Q);
            HandleKeyboard(1, KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.Return, KeyCode.RightShift);
        }
        private void HandleKeyboard(int player, KeyCode left, KeyCode right, KeyCode up, KeyCode down, KeyCode confirm, KeyCode back)
        {
            if (Input.GetKeyDown(back)) Back(player);
            var state = players[player];
            if (state.IsReady) return;
            int direction = Input.GetKeyDown(left) ? -1 : Input.GetKeyDown(right) ? 1 : 0;
            if (state.ChoosingVariation)
            {
                if (direction != 0) ChangeVariation(player, direction);
            }
            else
            {
                if (Input.GetKeyDown(up)) direction = -3;
                if (Input.GetKeyDown(down)) direction = 3;
                if (direction != 0) Preview(player, (state.CharacterIndex + direction + SlotCount) % SlotCount);
            }
            if (Input.GetKeyDown(confirm)) Confirm(player);
        }
        private void ReportMissingLayout(string path)
        {
            Debug.LogError("Character select is missing: " + path + ". Assign or restore the required element in the SelectScreen scene. Saved positions are preserved.", this);
            enabled = false;
        }
        private void Preview(int player, int index)
        {
            players[player].Preview(index);
            Refresh();
        }
        private void ChooseCharacter(int player, int index)
        {
            if (players[player].ChoosingVariation || players[player].IsReady) return;
            players[player].LockCharacter(index == fighters.Length ? UnityEngine.Random.Range(0, fighters.Length) : index);
            Refresh();
        }
        private int VariationCount(int player) => Mathf.Max(1, fighters[players[player].CharacterIndex].variations?.Length ?? 0);
        private void ChangeVariation(int player, int delta)
        {
            if (!players[player].ChoosingVariation) return;
            players[player].ChangeVariation(delta, VariationCount(player));
            Refresh();
        }
        private void Confirm(int player)
        {
            var state = players[player];
            if (!state.ChoosingVariation) { ChooseCharacter(player, state.CharacterIndex); return; }
            if (!state.Confirm()) return;
            Refresh();
            var fighter = fighters[state.CharacterIndex];
            if (player == 0) SelectionConfirmed?.Invoke(fighter, state.VariationIndex);
            PlayerSelectionConfirmed?.Invoke(player + 1, fighter, state.VariationIndex);
            if (BothPlayersReady && !readyNotificationSent)
            {
                readyNotificationSent = true;
                BothSelectionsConfirmed?.Invoke();
            }
        }
        private void Back(int player)
        {
            players[player].Back();
            readyNotificationSent = false;
            Refresh();
        }
        private void Refresh()
        {
            for (int player = 0; player < 2; player++) RefreshCard(player);
            SetText("Mode", BothPlayersReady ? "BOTH PLAYERS READY" : "TWO PLAYERS");
            for (int i = 0; i < SlotCount; i++)
            {
                var tile = ui.Find("Roster/Tile " + i);
                bool p1 = i == PlayerOne.CharacterIndex, p2 = i == PlayerTwo.CharacterIndex;
                var outline = tile.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.enabled = p1 || p2;
                    outline.effectColor = p1 && p2 ? Color.yellow : p1 ? playerOneColor : playerTwoColor;
                }
                tile.GetComponent<Button>().interactable = !PlayerOne.ChoosingVariation;
                var portrait = tile.Find("Portrait");
                var texture = i == fighters.Length ? null : fighters[i].portrait;
                SetTexture(portrait, texture, square: true);
                var initial = portrait.Find("Initial");
                if (initial != null) initial.gameObject.SetActive(texture == null);
            }
        }
        private void RefreshCard(int player)
        {
            var state = players[player];
            bool isRandom = state.CharacterIndex == fighters.Length;
            var fighter = isRandom ? randomOption : fighters[state.CharacterIndex];
            string card = CardPath(player);
            string variant = fighter.variations != null && fighter.variations.Length > 0 ? fighter.variations[state.VariationIndex] : "AGGRESSIVE";
            SetText(card + "/Player", "PLAYER " + (player + 1) + (state.IsReady ? " / READY" : ""));
            SetText(card + "/Name", fighter.name);
            SetText(card + "/Style", fighter.style);
            SetText(card + "/Variant", isRandom ? "SURPRISE ME" : variant);
            SetText(card + "/Art/Placeholder", isRandom ? "?" : string.IsNullOrEmpty(fighter.name) ? "?" : fighter.name.Substring(0,1));
            var art = ui.Find(card + "/Art").GetComponent<Image>();
            Texture2D texture = fighter.card;
            if (state.ChoosingVariation && fighter.variationCards != null && state.VariationIndex < fighter.variationCards.Length && fighter.variationCards[state.VariationIndex] != null)
                texture = fighter.variationCards[state.VariationIndex];
            SetTexture(art.transform, texture);
            art.color = texture == null ? fighter.color * .55f : Color.clear;
            SetOptionalActive(card + "/Art/Placeholder", texture == null);
            SetOptionalActive(card + "/Art/Mock label", texture == null);
            var accent = ui.Find(card + "/Accent")?.GetComponent<Image>();
            if (accent != null) accent.color = player == 0 ? playerOneColor : playerTwoColor;
            SetOptionalActive(card + "/Previous", state.ChoosingVariation);
            SetOptionalActive(card + "/Next", state.ChoosingVariation);
            SetOptionalInteractable(card + "/Previous", !state.IsReady);
            SetOptionalInteractable(card + "/Next", !state.IsReady);
            SetText(ConfirmPath(player) + "/Text", state.IsReady ? "READY" : state.ChoosingVariation ? "CONFIRM VARIATION" : "SELECT FIGHTER");
            SetOptionalInteractable(ConfirmPath(player), !state.IsReady);
        }
        private static void SetTexture(Transform parent, Texture2D texture, bool square = false)
        {
            var source = parent.Find("Source PNG");
            source.GetComponent<RawImage>().texture = texture;
            source.GetComponent<RawImage>().color = texture == null ? Color.clear : Color.white;
            var image = source.GetComponent<RawImage>();
            image.uvRect = new Rect(0, 0, 1, 1);
            if (texture != null)
            {
                float ratio = (float)texture.width / texture.height;
                source.GetComponent<AspectRatioFitter>().aspectRatio = square ? 1f : ratio;
                // Crop headshots to a square without stretching; full cards retain their original aspect.
                if (square)
                    image.uvRect = ratio > 1f
                        ? new Rect((1f - 1f / ratio) / 2f, 0, 1f / ratio, 1)
                        : new Rect(0, (1f - ratio) / 2f, 1, ratio);
            }
        }

        private void BindOptionalButton(string path, Action callback)
        {
            var button = ui.Find(path)?.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(() => callback());
        }

        private void SetOptionalInteractable(string path, bool interactable)
        {
            var button = ui.Find(path)?.GetComponent<Button>();
            if (button != null) button.interactable = interactable;
        }

        private void SetOptionalActive(string path, bool active)
        {
            var element = ui.Find(path);
            if (element != null) element.gameObject.SetActive(active);
        }

        private void SetText(string path, string value)
        {
            var text = ui.Find(path)?.GetComponent<Text>();
            if (text != null) text.text = value;
        }
    }
}
