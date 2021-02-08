using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

using static ZX.BattleManager;

namespace ZX {
    public class CardUI : MonoBehaviour {
        public int id;
        public int cardCount;
        private Text description;

        private void Awake() {
            description = transform.Find("Text").GetComponent<Text>();
        }

        public void SetCard(int id) {
            this.id = id;
            Card.Card card = BattleManager.instance.GetPlayer().cardManager.handCards[id];
            description.text = card.description;
        }
    }
}