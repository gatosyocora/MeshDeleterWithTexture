MeshDeleterWithTexture
テクスチャの箇所を指定することでメッシュの対応した箇所を削除できるEditor拡張です。

〇内容物
MeshDeleterWithTexture.cs : ソースコードです
GatoGUILayout.cs : gatosyocoraオリジナルのEditor拡張用GUIのソースコードです
Resources/TextureEdit.shader : テクスチャ補正用のシェーダーです
Resources/TextureEditMat.mat : テクスチャ補正用のマテリアルです
Resources/colorcheck2.compute : メインのコンピュートシェーダーです
Resources/getUVMap.compute : UVMap取得用のコンピュートシェーダーです


〇仕組み
複製したメッシュに対して
テクスチャで選択した範囲のuvに対応した頂点を削除して
新しいメッシュとして書き出しています。

〇使い方
1.  MeshDeleterWithTexture_vX.unitypackageをUnityプロジェクトにインポートします。
2.  メニューバーのGatoTool > MeshDeleter with Textureを選択します。
3.  "Renderer"に削除したい箇所を持つメッシュのオブジェクトを設定します。
	（SkinnedMeshRendererコンポーネントがついたオブジェクト）
4.  "Texture(Material)"のテクスチャ名を選択して削除したい箇所を持つテクスチャを選択してください。
5.  "DrawType"を"Pen"にするように選択してテクスチャの削除したいところを塗りつぶします。
	"Eraser"を選択することで一部の塗りつぶしを消したり, "Clear All Drawing"ですべての塗りつぶしを削除したりできます。
	また, "Pen/Eraser size"を変更することでペンの大きさを変えられます。
	塗りつぶしの際にマウスのホイールを動かしたり, "Scale"を変更することでテクスチャ表示画面の拡大縮小ができます。
	さらに右クリックをしながらマウスを動かすことで表示箇所を移動できます。
6.  削除したい箇所の塗りつぶしが終わったら"Output Mesh"の"SaveFolder"と"Name"でメッシュ書き出しの設定をおこないます。
7.  "Delete Mesh"を押すと塗りつぶしの場所に対応した箇所を削除したメッシュが書き出され, 自動的に設定されます。

メッシュ削除後, Ctrl+Zでメッシュ削除前に戻すこともできます。
また, SkinnedMeshRendererのMeshから別のメッシュに変更することもできます。
"Reset to Default Mesh"でfbxまたはPrefabに設定されているデフォルトのメッシュに戻すことができます。

"Import DeleteMask"は削除したい箇所をマスク画像で指定することができます。
マスク画像はテクスチャと同じ大きさで黒の部分が塗りつぶされます。
現在, png形式とjpg形式に対応しています。
また, "Export DeleteMask"で塗りつぶした箇所を黒色, それ以外を白色としたマスク画像をpng形式で出力できます。
"Drag & Drop DeleteMeskTexture"にマスク画像をドラッグアンドドロップしても読み込むことができます。
現在はpngとjpgに対応しています。

"Export UVMap"でUVマップテクスチャを出力できます。

"Inverse FillArea"で塗りつぶされた箇所が反転します（塗られている箇所→塗られていない、塗られていない箇所→塗られている）

"Undo Drawing"でPenでの塗りつぶしやInverse FillArea, Clear All Drawingを実行前の状態に戻せます。
Zキーでもこの機能を使えます。最大10回まで遡れます。

エディタを閉じたり、メッシュを変えたりしてもアバターの塗りつぶしが残っている場合は
マテリアルのテクスチャを元のものに戻してください。
テクスチャには直接書き込みをしていないので戻すことができます。

〇塗りつぶしのコツ
消したいメッシュ部分のUVマップの線を隠すように塗りつぶすといい感じにできます。
一部分のみ削除もできますが, 断面が綺麗にならないことが多いです。ご了承ください。

〇利用規約
本ツールおよびそのソースコードの著作権はgatosyocoraに帰属します。
本ツールを使ったことによって起こった問題に対して
gatosyocoraは一切の責任を負いかねますのであらかじめご了承ください。
また, 本ツールはMITライセンスで運用されます。詳しくはLICENSE.txtをご覧ください。

〇既知のバグ
* 使用時に「Assertion failed～」という警告メッセージがUnity左下に表示されますが処理には影響しません。
* 2018.4.20f1(もしかしたらこれ以前でも発生するかもしれない)でBuild PlatformをAndroidにしている場合、
ComputeShaderが動かず、本ツールが動作しない

〇動作保証環境
Unity 2018.4.20f1

〇更新履歴
v0.6	* 塗りつぶし箇所を反転する機能を追加
		* 塗りつぶしのUndo機能の追加
		* ドラッグ&ドロップでDeleteMaskTextureを読み込めるように
		* UIの調整
v0.5.2b * VRChat/Mobile/ToonLit.shaderなど頂点カラーを使うシェーダーで削除後のメッシュが黒くなる不具合を修正
v0.5.1b	* 同じテクスチャや同じマテリアルが適用されているメッシュにも対応
v0.5b	* ペンカーソルを表示
		* UVマップは表示されるように変更
		* UVマップテクスチャを出力する機能を追加
		* テクスチャを塗る機能をGPU処理に変更
		* メッシュ削除処理を高速化
v0.4b	* 拡大縮小機能を追加
		* マスク画像のエクスポート時にマテリアルからテクスチャが剥がれる不具合を修正
		* ペン選択時に色が黒色にリセットされる不具合を修正
		* UIを少し変更
v0.3b	* ポリゴン削除の方法を変更
v0.2b	* ペン色を追加（色は見えやすくするためで効果の違いはありません）
		* 塗りつぶした箇所がSceneビューのアバターに反映されるように
		* jpg形式もマスク画像として読み込めるように

〇連絡先
Twitter @gatosyocora
Discord gatosyocora#9575
