﻿using System.Collections;
using System.Collections.Generic;
using System.Text;
using System;
using UnityEngine;
using XLua;
using MotionFramework;
using MotionFramework.Console;
using MotionFramework.Resource;
using MotionFramework.Network;

public class LuaManager : ModuleSingleton<LuaManager>, IMotionModule
{
	[CSharpCallLua]
	public delegate string LanguageDelegate(string key);
	[CSharpCallLua]
	public delegate void NetMessageDelegate(int msgID, byte[] bytes);
	

	private readonly LuaEnv _luaEnv = new LuaEnv();
	private readonly TimerBase _tickTimer = new RepeatTimer(0, 1f);

	private LuaTable _gameTable;
	private Action _funStart;
	private Action _funUpdate;
	private LanguageDelegate _funLanguage;
	private NetMessageDelegate _funNetMessage;


	void IMotionModule.OnCreate(object createParam)
	{
	}
	void IMotionModule.OnStart()
	{
		_luaEnv.AddLoader(CustomLoaderMethod);
		_luaEnv.AddBuildin("rapidjson", XLua.LuaDLL.Lua.LoadRapidJson);
		_luaEnv.AddBuildin("lpeg", XLua.LuaDLL.Lua.LoadLpeg);
		_luaEnv.AddBuildin("pb", XLua.LuaDLL.Lua.LoadLuaProfobuf);

		// 初始化
		InitLuaScript();

		// Start
		_funStart?.Invoke();

		// 监听热更网络数据
		NetworkManager.Instance.HotfixPackageCallback += OnHandleHotfixPackage;
	}
	void IMotionModule.OnUpdate()
	{
		// Update
		_funUpdate?.Invoke();

		// Tick
		if (_tickTimer.Update(Time.unscaledDeltaTime))
			_luaEnv.Tick();
	}
	void IMotionModule.OnGUI()
	{
		AppConsole.GUILable($"[{nameof(LuaManager)}] Lua memory : {_luaEnv.Memroy}Kb");
	}

	/// <summary>
	/// 多语言查询
	/// </summary>
	/// <param name="key">关键字</param>
	/// <returns>返回查询结果</returns>
	public string Language(string key)
	{
		return _funLanguage?.Invoke(key);
	}

	/// <summary>
	/// 发送热更新网络消息
	/// </summary>
	public void SendHotfixNetMessage(int msgID, byte[] bytes)
	{
		NetPackage package = new NetPackage();
		package.IsHotfixPackage = true;
		package.MsgID = msgID;
		package.BodyBytes = bytes;
		NetworkManager.Instance.SendMessage(package);
	}

	/// <summary>
	/// 初始化LUA脚本
	/// </summary>
	private void InitLuaScript()
	{
		string resName = $"Lua/Game.lua";
		TextAsset asset = ResourceManager.Instance.SyncLoad<TextAsset>(resName);
		_gameTable = ExecuteScript(asset.bytes, "Game") as LuaTable;
		_funStart = _gameTable.Get<Action>("Start");
		_funUpdate = _gameTable.Get<Action>("Update");
		_funLanguage = _gameTable.Get<LanguageDelegate>("Language");
		_funNetMessage = _gameTable.Get<NetMessageDelegate>("HandleNetMessage");
	}

	/// <summary>
	/// 自定义文件加载方法
	/// </summary>
	private byte[] CustomLoaderMethod(ref string fileName)
	{
		// 同步加载LUA文件
		string resName = $"Lua/{fileName}.lua";
		TextAsset asset = ResourceManager.Instance.SyncLoad<TextAsset>(resName);
		if(asset == null)
		{
			AppLog.Log(ELogType.Warning, $"Failed to load lua file : {resName}");
			return null;
		}
		return asset.bytes;
	}

	/// <summary>
	/// Execute lua script directly
	/// </summary>
	private object ExecuteScript(byte[] scriptCode, string chunkName = "code")
	{
		var results = _luaEnv.DoString(Encoding.UTF8.GetString(scriptCode), chunkName);
		if (results == null) return null;
		if (results.Length == 1)
		{
			return results[0];
		}
		else
		{
			return results;
		}
	}

	private void OnHandleHotfixPackage(INetPackage pack)
	{
		NetPackage package = pack as NetPackage;
		_funNetMessage(package.MsgID, package.BodyBytes);
	}
}