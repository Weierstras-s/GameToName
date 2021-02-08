using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using static ZX.BattleManager;

namespace ZX {
    public class PlayerUI : MonoBehaviour {
        private Player player;

        public bool isAlive;

        [Header("基本数据:血量和行动力")]
        public float maxHealthPoint;
        public float healthPoint;
        public int maxActionPoint;
        public int actionPoint;
        public float maxMovePoint;
        public float movePoint;

        [Header("血条以及行动力条")]
        public Slider bloodBarSlider;
        public Slider actionPointBarSlider; //HP与AP的两个Slider

        [Header("左下方信息栏的HP，AP文字和人物头上的血条文字")]
        public Text hpStatusText;
        public Text hpInBarSliderText;
        public Text movePointStatusText;
        static public Text deltaMovePointText;
        public Text actionPointStatusText; //HP与AP的三个Text

        [Header("当前对象的SpriteRenderer")]
        public SpriteRenderer spriteRenderer;

        [Header("人物头上的伤害和效果信息")]
        public Vector3 damageTextOriginalPos;
        public Text damageText;
        public Text statusText;
        public Text healText;

        public int outlineType {
            set { UIController.instance.SetOutlineType(transform, value); }
        }

        public void ShowDamageText(float damage) {
            StartCoroutine(UIController.instance.ShowDamageText(this, damage));
        }
        public void ShowHealText(float heal) {
            StartCoroutine(UIController.instance.ShowHealText(this, heal));
        }

        private void Awake() {
            player = transform.GetComponent<Player>();

            bloodBarSlider = transform.Find("Canvas/HPSlider").GetComponent<Slider>();
            hpInBarSliderText = transform.Find("Canvas/HealthStateText").GetComponent<Text>();

            spriteRenderer = transform.GetComponent<SpriteRenderer>();
            damageText = transform.Find("Canvas/DamageText").GetComponent<Text>();
            statusText = transform.Find("Canvas/StatusText").GetComponent<Text>();
            healText = transform.Find("Canvas/HealText").GetComponent<Text>();

            hpStatusText = GameObject.Find("Canvas/PlayerStatus/HPInfo/HPData").GetComponent<Text>();
            actionPointBarSlider = GameObject.Find("Canvas/PlayerStatus/APBarCanvas/APSlider").GetComponent<Slider>();
            actionPointStatusText = GameObject.Find("Canvas/PlayerStatus/APInfo/APData").GetComponent<Text>();
            movePointStatusText = GameObject.Find("Canvas/number").GetComponent<Text>();
            deltaMovePointText = GameObject.Find("Canvas/number2").GetComponent<Text>();
        }

        private void Start() {
            maxHealthPoint = healthPoint = player.maxHitPoint;
            UIController.instance.SetBloodBarInfo(this);
        }

        private void Update() {
            maxActionPoint = player.maxActionPoint;
            actionPoint = player.actionPoint;
            maxMovePoint = player.maxMovePoint;
            movePoint = player.movePoint;
            UIController.instance.UpdateBloodText(this);
            if (healthPoint != player.hitPoint) {
                StartCoroutine(UIController.instance.UpdateBloodBar(this, player.hitPoint));
            }
            if (BattleManager.instance.IsCurrentPlayer(transform)) {
                UpdateCurrentUI();
            }
        }

        private void UpdateCurrentUI() {
            actionPointBarSlider.minValue = 0f;
            actionPointBarSlider.maxValue = maxActionPoint;
            actionPointBarSlider.value = actionPoint;
            actionPointStatusText.text = actionPoint.ToString();
            hpStatusText.text = healthPoint.ToString();
            movePointStatusText.text = movePoint.ToString();
        }
    }
}

