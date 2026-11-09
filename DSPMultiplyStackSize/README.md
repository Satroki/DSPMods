# Multiply Stack Size
Multiply stack sizes of all items include modded items. You can change the stack size individually.<br>
Allow to change mecha space warper slot stack size.<br>
This affect achievements or milestones. <br>
<br>
MODで追加された物を含む全てのアイテムのスタックサイズを変更します。スタック数を個別に変更することできます。<br>
メカの空間湾曲器(space warper)スロットのスタックサイズも変更できます。<br>
実績やマイルストーンに影響します。<br>
<br>
## Features　特徴
Press the button at the top of the inventory window to open the stack size editor.<br>
Select an item and edit stack size and press "Apply" button and record it.  <br>
The changed stack sizes are displayed in orange.<br>
Press the "Reset" button to initialize all stack counts. (Not the vanilla standard size, but value multiplied by the set magnification)<br>
It is recorded in "stackSizeDictionary.json" in BepInEx/Config.<br>
<br>
You can change the settings in the config file.steamapps/common/Dyson Sphere Program/config/Appun.DSP.plugin.MultiplyStacSize.cfg<br>
- Stack Size Multiplier (StackSizeMultiplier:default 5)<br>
- change Mecha Space Warper Slot Stack Size (changeWarperSlotStackSize:default on)<br>

インベントリウインドウの上にあるボタンを押すと、スタック数編集画面が開きます。<br>
アイテムを選択してスタック数を編集変更して「適用する」ボタンを押すと記録されます。変更したスタック数は黄色で表示されます。<br>
「リセット」ボタンを押すと、全てのスタック数を初期化します。（バニラ標準ではなく、設定の倍率をかけたもの）<br>
変更されたスタック数は「stackSizeDictionary.json」に記録されます。BepInEx/Configフォルダにあります。<br>
<br>
コンフィグファイルで設定を変更することができます。steamapps/common/Dyson Sphere Program/config/Appun.DSP.plugin.MultiplyStacSize.cfg<br>
- スタックサイズの倍率（StackSizeMultiplier:デフォルト5）<br>
- メカの空間湾曲器(warper)スロットのスタックサイズも変更するか(changeWarperSlotStackSize:デフォルトオン）<br>
<br>
## How to install　インストール方法
1. Install BepInEx<br>
   BepInExをインストールします。<br>
2. Drag DSPMultiplyStacSize.dll into steamapps/common/Dyson Sphere Program/BepInEx/plugins<br>
   DSPMultiplyStacSize.dllをsteamapps/common/Dyson Sphere Program/BepInEx/pluginsに配置します。<br>
<br>
## Contact 問い合わせ先
If you have any problems or suggestions, please contact DISCORD MSP Modding server **Appun#8284**.<br>
不具合、改善案などありましたら、DISCORD「DysonSphereProgram_Jp」サーバー**Appun#8284**までお願いします。<br>
<br>
## Change Log
### v1.0.13
- Supported the game Version 0.10.28.21014. Allow to change mecha space warper slot stack size.<br>
- ゲームバージョン0.10.28.21014に対応しています。メカの空間湾曲器(space warper)スロットのスタックサイズは変更できるようになりました。
### v1.0.12
- Supported the game Version 0.10.28.20959. Not allow to change mecha space warper slot stack size.<br>
- ゲームバージョン0.10.28.20959に対応しましたが、メカの空間湾曲器(space warper)スロットのスタックサイズは変更できません。
### v1.0.11
- Supported the game version 0.9.27.14553. ゲームバージョン0.9.27.14553に対応しています。
### v1.0.10
- Changed the save folder so that an update doesn't delete the "stackSizeDictionary.json". アップデートしても「stackSizeDictionary.json」ファイルが削除されないように、保存フォルダを変更しました。
- If you are using this mod, please move "stackSizeDictionary.json" to BepInEx/Config before applying this update.　このMODを使用中の方は、このアップデートを適応する前に、「stackSizeDictionary.json」をBepInEx/Configに移動してください。
### v1.0.9
- Fixed an error when pressing the Apply button without selecting an item. アイテムを選択せずに適用ボタンを押したときのエラーを修正しました。
### v1.0.8
- Resolved a conflict with the mod "BlueprintTweaks". MOD「BlueprintTweaks」と同時に使う場合に生じるエラーを解決しました。
### v1.0.7
- Now possible to change the stack size individually.　スタック数を個別に変更することできるようになりました。
- Confirmed that it is compatible with game version 0.9.24.11286. ゲームバージョン0.9.24.11286に対応していることを確認しました。
- Published in GitHub. GitHubに公開しました。
### v1.0.6
- Reduce the load 負荷を軽減しました。
### v1.0.5
- Allow to change mecha waper slot stack size. メカの空間湾曲器(space warper)スロットのスタック数も変更できるようになりました。
### v1.0.4
- Fixed applying again if loading fails. 起動直後のロードに失敗すると、再度乗算していたバグを修正しました。
### v1.0.3
- Now this work also modded items.　MODのアイテムにきちんと適応されるようになりました。
### v1.0.2
- Fixed a fatal bug. this dont work modded items now.　スタック数が変更されないという致命的なバグを解決しました。まだMODアイテムには適応されません。
### v1.0.1
- This work with also modded items.but did't work.  MODのアイテムにも適応したつもりがされていませんでした。