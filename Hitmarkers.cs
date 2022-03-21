//idk wich using should i use since i'm not using vs , so i just use all of them :troll:
//My dumbest trick is to compile it without the 'using', and the error will tell me which I should put them back, did it one by one :rofl:
using Microsoft.Xna.Framework; //Vector2
using Microsoft.Xna.Framework.Graphics; //Texture2D
using System.Collections.Generic; //List<>
using System.ComponentModel; //DefaultValueAttribute
using Terraria; //Item
using Terraria.ModLoader; //Mod
using Terraria.ModLoader.Config; //ModConfig
using System.Reflection; // reflection
using MonoMod.RuntimeDetour; // detour
using MonoMod.RuntimeDetour.HookGen; // detour
using Terraria.ID; // id
//custom config
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Linq;
using System.Runtime.Serialization;
using Terraria.ModLoader.Config.UI;
using Terraria.UI;
using System.Collections.ObjectModel;
using System.IO;
using Terraria.Utilities;

//Changed the arrangement of some code personally, I apologize if it somehow became hard to read

//all in one file moment
//Not anymore >:D
//Well not anymore again >:D
namespace Hitmarkers
{
	public class Hitmarkers : Mod // all of this are handled in Hitmarker class
	{
		public override void Load ()
		{Hitmarker.Load ();}
		
		public override void Unload ()
		{Hitmarker.Unload ();}
		
		public override void MidUpdatePlayerNPC ()
		{Hitmarker.UpdateAll ();}
		
		public override void PreSaveAndQuit ()
		{Hitmarker.Reset ();}
	}
	
	public class EpicPlayer : ModPlayer
	{
		public override void OnHitNPC (Item item, NPC target, int damage, float knockback, bool crit)
		{
			if (MyConfig.get.localplayer && player.whoAmI != Main.myPlayer)
			{return;}
			
			if (MyConfig.get.Melee)
			{Hitmarker.New (target.Center,(byte)(crit ? 1 : 0));}
		}
		
		public override void OnHitNPCWithProj (Projectile projectile, NPC target, int damage, float knockback, bool crit)
		{
			if (MyConfig.get.localplayer && player.whoAmI != Main.myPlayer)
			{return;}
		
			if (projectile.ranged || MyConfig.get.AnyProj)
			{Hitmarker.New ((MyConfig.get.positionTarget ? target.Center : projectile.Center),(byte)(crit ? 1 : 0));}
		}
		
		//My dumbass self tried shoving OnCollideNPC in here for Miss Effect, learned things the hard way and realized it was for projectiles :skull:
	}

	public class Hitmarker
	{
		Vector2 position;
		// timeleft is now a byte, 0.000000000000001% faster :troll:
		byte timeLeft;
		byte type;
		
		public static List<Hitmarker> hitmarkers = new List<Hitmarker> (); //reject array, return to List

		public static void Load ()
		{
			hitmarkers = new List<Hitmarker> ();
			//apply detour
			On.Terraria.Main.DrawDust += DrawDustPatch;
			On_OnTileCollide += OnTileCollidePatch;
		}
		
		public static void Unload ()
		{
			hitmarkers = null;
			//de-apply detour, this isnt neccesary thought, just incase if something starts exploding
			On.Terraria.Main.DrawDust -= DrawDustPatch;
			On_OnTileCollide -= OnTileCollidePatch;
		}
		// uses detour instead of globalproj so it doesnt broke any mod ( i think this is illegal thought, but i dont care , i can do whatever i want , look at me goo !!)
		public static event Hook_OnTileCollide On_OnTileCollide {
			add => HookEndpointManager.Add<Hook_OnTileCollide>(typeof(Mod).Assembly.GetType("Terraria.ModLoader.ProjectileLoader").GetMethod("OnTileCollide", BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic), value);
			remove => HookEndpointManager.Remove<Hook_OnTileCollide>(typeof(Mod).Assembly.GetType("Terraria.ModLoader.ProjectileLoader").GetMethod("OnTileCollide", BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic), value);
		}
		public delegate bool orig_OnTileCollide(Projectile projectile, Vector2 oldVelocity);
		public delegate bool Hook_OnTileCollide(orig_OnTileCollide orig, Projectile projectile, Vector2 oldVelocity);
		public static bool OnTileCollidePatch(orig_OnTileCollide orig,Projectile projectile, Vector2 oldVelocity) {
			if ( projectile.friendly && (projectile.ranged || MyConfig.get.AnyProj) && MyConfig.get.MissMarker && (!MyConfig.get.localplayer || (MyConfig.get.localplayer && projectile.owner == Main.myPlayer))) 
			{New(projectile.Center,2);}
			return orig(projectile,oldVelocity);
		}

		public static void DrawDustPatch (On.Terraria.Main.orig_DrawDust orig,Main main) //the detour
		{
			orig (main);
			//make it draw after all dust is drawn
			Main.spriteBatch.Begin (SpriteSortMode.Deferred, null, null, null, null, null, Main.GameViewMatrix.ZoomMatrix); 
			DrawAll ();
			Main.spriteBatch.End ();
		}
		
		public static void Reset () //reset hitmarkers
		{hitmarkers = new List<Hitmarker> ();}
		
		public bool Update ()
		{
			if (timeLeft < 25)
			{
				timeLeft += (byte)MyConfig.get.timeLeft;
				return true;
			}
			//this exist to prevent out of bounds, frames maybe become glitched bc the timeleft is bigger than 25
			if (timeLeft > 25) {timeLeft = 25;}
			return false;
			
			/* Commented this just in-case, replace with this if above doesn't work
			if (timeLeft >= 25) {return false;}
			timeLeft += MyConfig.get.timeLeft;
			//prevent some out of bounds
			if (timeLeft > 25) {timeLeft = 25;}
			return true;
			*/
		}
		
		public static void New (Vector2 position,byte type)
		{
			//doesnt spawn if its too far away
			float maxDis = 1500f; // modify this 
			Vector2 screen = Main.screenPosition - new Vector2(Main.screenWidth/2f,Main.screenHeight/2f);
			if (Vector2.Distance(screen,position) > maxDis) {
				if (MyConfig.get.debug)
				{Main.NewText($"Distance too high ! {Vector2.Distance(screen,position)}");} //Gives you freedom to squash bugs
				return;
			}
			hitmarkers.Add (new Hitmarker (position, 0, type));	

			if (hitmarkers.Count > MyConfig.get.maxMarker) //I love mods that gives players option to optimize things in-game  ( me too :thiscat: )
			{hitmarkers.RemoveAt (0);}

			if (MyConfig.get.debug)
			{Main.NewText ("Hit ["+hitmarkers.Count+"] (X: "+position.X+" | Y: "+position.Y+") (Type: "+type+")");} //Gives you freedom to squash bugs
		}
		
		public static void UpdateAll () //update all
		{
			if (hitmarkers == null || hitmarkers.Count == 0) //safe code moment (1)
			{return;}
		
			var remove = new List<Hitmarker> ();
			
			foreach (var item in hitmarkers)
			{
				if (!item.Update ())
				{remove.Add (item);}
			}
			
			foreach (var i in remove)
			{
				if (MyConfig.get.debug)
				{Main.NewText ("Hitmark removed");}
			
				hitmarkers.Remove (i);
			}
		}
		
		public static void DrawAll () //draw all
		{
			if (hitmarkers == null || hitmarkers.Count == 0) //safe code moment (2)
			{return;}
		
			foreach (var item in hitmarkers)
			{item.Draw ();}
		}
		
		public void Draw () //the draw method
		{
			int frame = timeLeft/5;
			float num = (float)timeLeft/25f;

			string path = "Hitmarkers/Texture/"+(MyConfig.get.smooth ? "Smooth/" : "Rough/");

			Texture2D texture = ModContent.GetTexture(path+MyConfig.get.hitTexture);
			Color color = Color.Lerp (MyConfig.get.Color.HitStart,MyConfig.get.Color.HitEnd,num);
			
			if (type == 1)
			{
				texture = ModContent.GetTexture(path+MyConfig.get.critTexture);
				color = Color.Lerp (MyConfig.get.Color.CritStart,MyConfig.get.Color.CritEnd,num);
			}
			if (type == 2) {
				texture = ModContent.GetTexture(path+MyConfig.get.missTexture);
				color = Color.Lerp (MyConfig.get.Color.MissStart,MyConfig.get.Color.MissEnd,num);
			}
			
			Rectangle rec = GetFrame (texture,frame,5);
			float scale = MyConfig.get.scale;

			//removed smoll to big (it was buggy)
			if (MyConfig.get.scaleAuto)
			{
				scale = ((num - 1f)*-1f)*MyConfig.get.scale;
			}
	
			
			Main.spriteBatch.Draw (texture, position - Main.screenPosition, rec, color, 0f, rec.Size ()/2f, scale, SpriteEffects.None, 0);
		}
		
		public Hitmarker (Vector2 position, byte timeLeft,byte type) //the constructor
		{
			this.position = position;
			this.timeLeft = timeLeft;
			this.type = type;
		}
		
		public static Rectangle GetFrame (Texture2D Texture, int frame, int maxframe) //an extension i imported
		{
			int frameHeight = Texture.Height / maxframe;
			int startY = frameHeight * frame;
			return new Rectangle (0, startY, Texture.Width, frameHeight);
		}
	}
	/*

	reserved bc some mod may also uses GlobalProjectile.OnTileCollide (some modded proj may return false in OnTileCollide)
	replaced by hook detour ( like how i detour ModifyScreenPosition in ObamaCamera mod, for other mod compability)

	public class GlobalProj : GlobalProjectile //Temporarily added this for miss effect, Mirsario said that using projectile for effect was a dumb idea, but I'm a dumbass, so jokes on him >:3
	{	
		public override bool OnTileCollide(Projectile projectile, Vector2 oldVelocity)
		{
			Player localplayer = Main.player[projectile.owner];
			if (projectile.ranged)
			{
				Projectile.NewProjectile (projectile.position.X, projectile.position.Y, 0f, 0f, mod.ProjectileType ("MissEffect"), 0, 0f, localplayer.whoAmI);
			}
			return true;
		}
	}
	*/
	
	[Label("Configs")]
	public class MyConfig : ModConfig //Changed some of description, because why not >:D
	{
		public override ConfigScope Mode => ConfigScope.ClientSide; //the config is for client not server
		public static MyConfig get => ModContent.GetInstance<MyConfig> (); //used to get the config instance
		
		[Header("Visuals")]

		[SeparatePage]
		[Label("Hitmarker Color")]
		public HitmarkerColorData Color = new HitmarkerColorData();

		[Label("Hitmarker Scale")]
		[Tooltip("Change the size of a hitmarker")]
		[Range(0.1f, 4f)]
		[Increment(0.1f)]
		[DefaultValue(1f)]
		[Slider] 
		public float scale;

		[Label("Hitmarker Time Multiplier")]
		[Tooltip("Multiply how fast the hitmarker animates")]
		[Range(1,20)]
		[Increment(1)]
		[DefaultValue(5)]
		[Slider] 
		public int timeLeft;

		[Label("Hitmarker Auto Scale")]
		[Tooltip("Slowly change the scale from big to smoll")]
		//[OptionStrings(new string[] { "None", "Big to })]
		[DefaultValue(false)]
		public bool scaleAuto;

		[Label("Hit Texture")]
		[DefaultValue(TextureEnum.Ball)]
		[Tooltip("The Hit Texture \n(default to 'Ball')")]
		public TextureEnum hitTexture;

		[Label("Crit Texture")]
		[DefaultValue(TextureEnum.Cross_1)]
		[Tooltip("The Crit Texture \n(default to 'Cross 1')")]
		public TextureEnum critTexture;

		[Label("Miss Texture")]
		[DefaultValue(TextureEnum.Cross_2)]
		[Tooltip("The Miss Texture \n(default to 'Cross 2')")]
		public TextureEnum missTexture;
		
		/*
		[Label("Hit Texture")]
		[Tooltip("the hit texture")]
		[Slider]
		[OptionStrings(new string[] { "Cross_1", "Cross_2","Cross_3","Ball"})]
		[DefaultValue("Ball")]
		public string hitTexture;

		[Label("Crit Texture")]
		[Tooltip("the crit texture")]
		[Slider]
		[OptionStrings(new string[] { "Cross_1", "Cross_2","Cross_3","Ball"})]
		[DefaultValue("Cross_1")]
		public string critTexture;

		[Label("Miss Texture")]
		[Tooltip("the miss texture")]
		[Slider]
		[OptionStrings(new string[] { "Cross_1", "Cross_2","Cross_3","Ball"})]
		[DefaultValue("Cross_2")]
		public string missTexture;
		*/
		
		[Label("Smooth Texture")]
		[Tooltip("Make the hitmarker uses smooth texture")]
		[DefaultValue(false)]
		public bool smooth;

		[Header("Additional Options")]

		[Label("Miss Hitmarker")]
		[Tooltip("Create a miss hitmarker whenever the projectile hits a tile")]
		[DefaultValue(true)]
		public bool MissMarker;

		[Label("Snap hitmark position to target")]
		[Tooltip("Set the hitmark position on the target instead of where projectile hits")]
		[DefaultValue(false)]
		public bool positionTarget;

		[Label("Apply to any projectiles")]
		[Tooltip("Projectiles other than a bullet or arrow will also have a hitmark")]
		[DefaultValue(false)]
		public bool AnyProj;

		[Label("Apply to melee")]
		[Tooltip("Applies hitmarker to melee attacks")]
		[DefaultValue(false)]
		public bool Melee;

		[Label("Apply only to Local Player")]
		[Tooltip("Spawns hitmarkers only for 'localplayer'")]
		[DefaultValue(false)]
		public bool localplayer;

		[Label("Show debug")]
		[Tooltip("Show some values in chat")]
		[DefaultValue(false)]
		public bool debug;

		[Label("Max hitmarker")]
		[Tooltip("Maximum amount allowed for shown hitmarkers (set to low if it causes fps drop)")]
		[Range(100, 1000)]
		[Increment(100)]
		[DefaultValue(500)]
		[Slider] 
		public int maxMarker;
	}
	public class HitmarkerColorData
	{
		[Header("Crit Color")]

		[Label("Crit Start Color")]
		[Tooltip("The start of crit color")]
		[DefaultValue(typeof(Color), "255,0,0,255")]
		public Color CritStart = new Color(255,0,0,255);

		[Label("Crit End Color")]
		[Tooltip("The end of crit color")]
		[DefaultValue(typeof(Color), "255,242,0,0")]
		public Color CritEnd = new Color(255,242,0,0);

		[Header("Hit Color")]

		[Label("Hit Start Color")]
		[Tooltip("The start of hit color")]
		[DefaultValue(typeof(Color), "255,0,0,255")]
		public Color HitStart = new Color(255,0,0,255);

		[Label("Hit End Color")]
		[Tooltip("The end of hit color")]
		[DefaultValue(typeof(Color), "255,242,0,0")]
		public Color HitEnd = new Color(255,242,0,0);

		[Header("Miss Color")]
		
		[Label("Miss Start Color")]
		[Tooltip("The start of miss color")]
		[DefaultValue(typeof(Color), "255,255,255,255")]
		public Color MissStart = new Color(255,255,255,255);

		[Label("Miss End Color")]
		[Tooltip("The end of miss color")]
		[DefaultValue(typeof(Color), "255,255,255,0")]
		public Color MissEnd = new Color(255,255,255,0);

		public override bool Equals(object obj) {
			if (obj is HitmarkerColorData other)
				return CritStart == other.CritStart && CritEnd == other.CritEnd &&
				HitStart == other.HitStart && HitEnd == other.HitEnd &&
				MissStart == other.MissStart && MissEnd == other.MissEnd;
			return base.Equals(obj);
		}
		//public override string ToString() => "Hitmarker Color";
		public override int GetHashCode() {
			return new {CritStart,CritEnd,HitStart,HitEnd,MissStart,MissEnd }.GetHashCode();
		}
	}

	// custom config , this is pain

	[JsonConverter(typeof(StringEnumConverter))]
	[CustomModConfigItem(typeof(TextureElement))]
	public enum TextureEnum
	{
		// you can add ur own texture by adding another enum in here
		// add "_" as the replacement of space
		// after that put another texture in the texture folder with the same name, both at "rough" and "smooth" folder
		// make sure the texture has 5 vertical frame :thiscat:

		// Uncomment this for Example :
		//Example_Cross,
		Box,
		PoorlyDrawn_Cross,
		Cross_1,
		Cross_2,
		Cross_3,
		Ball
	}
	// This custom config UI element shows a completely custom config element that handles setting and getting the values in addition to custom drawing.
	internal class TextureElement : ConfigElement
	{
		Texture2D circleTexture;
		string[] valueStrings;

		public override void OnBind() {
			base.OnBind();
			circleTexture = Terraria.Graphics.TextureManager.Load("Images/UI/Settings_Toggle");
			valueStrings = Enum.GetNames(memberInfo.Type);
			TextDisplayFunction = () => memberInfo.Name + ": " + GetStringValue();
			if (labelAttribute != null) {
				TextDisplayFunction = () => labelAttribute.Label + ": " + GetStringValue();
			}
		}

		void SetValue(TextureEnum value) => SetObject(value);

		TextureEnum GetValue() => (TextureEnum)GetObject();

		string GetStringValue() {
			return valueStrings[(int)GetValue()].Replace("_"," ");
		}

		public override void Click(UIMouseEvent evt) {
			base.Click(evt);
			SetValue(GetValue().NextEnum());
		}

		public override void RightClick(UIMouseEvent evt) {
			base.RightClick(evt);
			SetValue(GetValue().PreviousEnum());
		}
		static float timer;
		// i hate ui , i hate ui , i hate ui , i hate ui , i hate ui , i hate ui , i hate ui , i hate ui , i hate ui , i hate ui , i hate ui , i hate ui.
		public override void Draw(SpriteBatch spriteBatch) {
			/*
			if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Left)) {
				x--;
			}
			if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Right)) {
				x++;
			}
			if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Up)) {
				y--;
			}
			if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Down)) {
				y++;
			}
			*/
			base.Draw(spriteBatch);
			timer += 0.5f;
			//reset at 696969f
			if (timer >= 696969f) {
				timer = 0;
			}
			string path = "Hitmarkers/Texture/"+(MyConfig.get.smooth ? "Smooth/" : "Rough/");
			path += GetStringValue().Replace(" ","_");
			int frame = (int)timer % 25;
			frame /= 5;
			CalculatedStyle dimensions = base.GetDimensions();
			Texture2D texture = ModContent.GetTexture(path);
			Vector2 pos = new Vector2(dimensions.X + dimensions.Width  -36,dimensions.Y + 15);
			Rectangle rec = Hitmarker.GetFrame(texture,(int)frame,5);
			spriteBatch.Draw(texture, pos, rec, Color.White, 0f, rec.Size ()/2f, 0.7f, SpriteEffects.None, 0);
		}
	}
}