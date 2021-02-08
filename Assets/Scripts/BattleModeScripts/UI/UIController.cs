using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using ZX.Templates.Singleton;

namespace ZX {
    public class UIController : MonoSingleton<UIController> {
        [Header("存放选中和未选中时的材质的数组")]
        public List<Material> material;

        [Header("血条变化的持续时间和变化快慢")]
        public float sliderTime = 1f;
        public float sliderSpeed = 5f;
        public float textTime = 2f;
        public float textVelocity = 0.2f;

        [Header("出战顺序表ListCages")]
        public Image[] attackListImage;

        [Header("四张卡牌所在的CardCages(网格布局组)")]
        public Transform cardListCages;
        public GameObject card;

        public Text buffListText;

        public void SetCards() {
            int childCount = cardListCages.childCount;
            for (int i = 0; i < childCount; i++) {
                Destroy(cardListCages.GetChild(i).gameObject);
            }
            for (int i = 0; i < BattleManager.instance.GetPlayer().cardManager.handCards.Count; i++) {
                var cardUI = Instantiate(card, cardListCages).GetComponent<CardUI>();
                cardUI.SetCard(i);
            }
        }

        #region 被选中时改变的描边部分，用于人物和卡牌
        /// <summary> 设置边框样式 </summary>
        public void SetOutlineType(Transform item, int type) {
            item.Find("Sprite").GetComponent<SpriteRenderer>().material = material[type];
        }
        #endregion

        #region HP人物头顶血条信息部分

        /// <summary> 进入战斗前给设置血量条信息 </summary>
        public void SetBloodBarInfo(PlayerUI thisPlayer) {
            thisPlayer.bloodBarSlider.minValue = 0f;
            thisPlayer.bloodBarSlider.maxValue = thisPlayer.maxHealthPoint;
            thisPlayer.bloodBarSlider.value = thisPlayer.healthPoint;
            UpdateBloodText(thisPlayer);
        }

        /// <summary> 血量发生变化时修改血条 </summary>
        /// <param name="hp"> 作用后的hp值 </param>
        public IEnumerator UpdateBloodBar(PlayerUI thisPlayer, float hp) {
            float preHealthPoint = thisPlayer.healthPoint;
            thisPlayer.healthPoint = hp;
            float duration = 0f;
            while (duration < sliderTime) {
                duration += sliderSpeed * Time.deltaTime;
                thisPlayer.bloodBarSlider.value = Mathf.Lerp(preHealthPoint, hp, duration);
                yield return new WaitForEndOfFrame();
            }
            thisPlayer.bloodBarSlider.value = hp;
        }

        /// <summary> 修改血条上的文本 </summary>
        public void UpdateBloodText(PlayerUI thisPlayer) {
            thisPlayer.hpInBarSliderText.text = $"{thisPlayer.healthPoint} / {thisPlayer.maxHealthPoint}";
        }

        #endregion

        #region 伤害与效果信息

        /// <summary>
        /// 伤害显示
        /// </summary>
        public IEnumerator ShowDamageText(PlayerUI thisPlayer, float damage) {
            int directionModify = 1;
            float duration = 0f;
            /*if (currentPerson.position.x < transform.position.x){
                directionModify = 1;
            }else{
                directionModify = -1;
            }*/
            thisPlayer.damageText.text = $"-{damage}";
            Vector3 direction = new Vector3(Random.Range(0, 100), Random.Range(0, 100), 0) * directionModify;
            thisPlayer.damageText.gameObject.SetActive(true);
            while (duration < textTime) {
                duration += sliderSpeed * Time.deltaTime;
                thisPlayer.damageText.transform.Translate(direction.normalized * textVelocity * Time.deltaTime, Space.World);
                yield return new WaitForEndOfFrame();
            }
            thisPlayer.damageText.gameObject.SetActive(false);
            thisPlayer.damageText.transform.position = thisPlayer.damageTextOriginalPos;
        }

        /// <summary>
        /// 回血显示
        /// </summary>
        public IEnumerator ShowHealText(PlayerUI thisPlayer, float heal) {
            thisPlayer.healText.text = $"+{heal}";
            thisPlayer.healText.gameObject.SetActive(true);
            float duration = 0f;
            while (duration < textTime) {
                duration += sliderSpeed * Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }
            thisPlayer.healText.gameObject.SetActive(false);
        }

        /// <summary>
        /// 状态显示
        /// </summary>
        public IEnumerator ShowStatusText(PlayerUI thisPlayer, string status) {
            thisPlayer.statusText.text = status;
            thisPlayer.gameObject.SetActive(true);
            float duration = 0f;
            while (duration < textTime) {
                duration += sliderSpeed * Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }
            thisPlayer.gameObject.SetActive(false);
        }
        #endregion

        #region 攻击顺序显示信息
        public void InitializeAttackListImage(List<Transform> attackList) {
            for (int i = 0; i < attackList.Count; i++) {
                attackListImage[i].sprite = attackList[i].GetComponent<Image>().sprite;
                attackListImage[i].gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 更新游戏上方行动顺序显示
        /// </summary>
        /// <param name="currentPersonIndex">当前行动角色在排序后角色行动顺序List中的位置</param>
        /// <param name="prePersonIndex">上一个角色在排序后角色行动顺序List中的位置</param>
        public void UpdateAttackListImage(int currentPersonIndex, int prePersonIndex) {
            attackListImage[prePersonIndex].material = material[0];//将List中上一轮行动的人的绿色轮廓去掉
            attackListImage[currentPersonIndex].material = material[1];//将当前行动的图标换成绿色轮廓
        }

        /// <summary>
        /// 给挂掉的对象在List中做红色轮廓
        /// </summary>
        /// <param name="deadPersonIndex">挂掉的角色在排序后角色行动顺序List中的位置</param>
        public void TickDeadPerson(int deadPersonIndex) {
            attackListImage[deadPersonIndex].material = material[2];
        }
        #endregion

        protected override void Awake() {
            base.Awake();
			material = new List<Material> {
				Resources.Load<Material>("Material/Line"),
				Resources.Load<Material>("Material/SelectedOutLine"),
				Resources.Load<Material>("Material/SelectedOutLine2")
			};
			cardListCages = GameObject.Find("Canvas/CardUI/CardCages").transform;
            buffListText = GameObject.Find("Canvas/BuffList").GetComponent<Text>();
        }
    }
}

