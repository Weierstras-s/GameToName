using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ZX {
	public class Buff {
		public string name;
		private int duration;

		public enum TimeStamp { START, END }
		public TimeStamp actTime;

		public Buff(int duration) {
			this.duration = duration;
		}

		/// <summary> 给当前玩家作用Buff </summary>
		public virtual void Activate(Player player) => --duration;

		/// <summary> Buff生效后是否存在 </summary>
		public virtual bool Exists() => duration > 0;

		/// <summary> 是否能行动 </summary>
		public bool allowAction = true;

		/// <summary> 是否能专注 </summary>
		public bool allowConcentrate = true;

		/// <summary> 是否能移动 </summary>
		public bool allowMove = true;

		public virtual float NewAttack(float attack) => attack;
		public virtual float NewDefence(float defence) => defence;

	}

}
