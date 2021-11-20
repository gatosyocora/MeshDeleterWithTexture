# MeshDeleterWithTexture

テクスチャの箇所を指定することでメッシュの対応した箇所を削除できるEditor拡張

## 仕組み

複製したメッシュに対して, テクスチャで選択した範囲のuvに対応した頂点を削除して, 新しいメッシュとして書き出す

## 使い方

1. MeshDeleterWithTexture_vX.unitypackageをUnityプロジェクトにインポートする
2. メニューバーのGatoTool > MeshDeleter with Textureを選択する
3. "Renderer"に削除したい箇所を持つメッシュのオブジェクトを設定する（SkinnedMeshRendererコンポーネントがついたオブジェクト）
4. "Texture(Material)"のテクスチャ名を選択して削除したい箇所を持つテクスチャを選択する
5. 以下の操作をおこなって削除したい箇所を指定する
   - "DrawType:描画ツール"を"Pen:ペン"にするように選択してテクスチャの削除したいところを塗りつぶす
   - "Eraser:消しゴム"を選択することで一部の塗りつぶしを消したり, "Clear All Drawing:塗りをすべて消す"ですべての塗りつぶしを削除したりできる
   - "Select:選択"を選択することで線で囲った範囲を選択できる。"Apply SelectArea:選択範囲を塗る"で選択中の範囲を塗りつぶしできる
   - "Pen/Eraser size:ペン/消しゴム 大きさ"を変更することでペンの大きさを変えられる
   - 塗りつぶしの際にマウスのホイールを動かしたり, "Scale:拡大/縮小"を変更することでテクスチャ表示画面の拡大縮小ができる
   - 右クリックをしながらマウスを動かすことで表示箇所を移動できる
6. 削除したい箇所の塗りつぶしが終わったら"Output Mesh:書き出すメッシュ"の"SaveFolder:保存するフォルダ"と"Name:名前"でメッシュ書き出しの設定をおこなう
7. "Delete Mesh:メッシュを削除"を押すと塗りつぶしの場所に対応した箇所を削除したメッシュが書き出され, 自動的に設定される

## その他機能

### Revert:メッシュ変形の取り消し

- メッシュ削除後, Ctrl+Zでメッシュ削除前に戻せる
- SkinnedMeshRendererのMeshから別のメッシュに変更できる
- "Revert Mesh to Prefab:プレハブのメッシュに戻す"でfbxまたはPrefabに設定されているデフォルトのメッシュに戻せる
- "Revert Mesh to previously:ひとつ前のメッシュに戻す"で削除前のメッシュに戻せる

### DeleteMask:削除マスク

- "Import DeleteMask:削除用テクスチャを読み込む"は削除したい箇所をマスク画像で指定できる
  - マスク画像はテクスチャと同じ大きさで黒の部分が塗りつぶされる
  - 現在, png形式とjpg形式に対応
- "Export DeleteMask:削除用テクスチャを書き出す"で塗りつぶした箇所を黒色, それ以外を白色としたマスク画像をpng形式で出力できる
- "Drag & Drop DeleteMeskTexture:削除用テクスチャをドラッグ$ドロップ"にマスク画像をドラッグアンドドロップしても読み込める
  - 現在はpngとjpgに対応している

### UVMap:UVマップ

- "Export UVMap:UVマップを書き出す"でUVマップテクスチャを出力できる
- "UVMap LineColor:UVマップ線色"でUVマップの線の色を変更できる

### Inverse:反転

- "Inverse FillArea:塗りを反転"で塗りつぶされた箇所が反転する
  - 塗られている箇所->塗られていない
  - 塗られていない箇所->塗られている

### Undo:塗り操作の取り消し

- "Undo Drawing:塗りをひとつ戻す"でPenでの塗りつぶしやInverse FillArea, Clear All Drawingを実行前の状態に戻せる
  - Ctrl+Zキーでもこの機能を使用
  - 最大10回までさかのぼれる

### Straight Line:直線

- 直線の線を引くことができる
  - 左右いずれかのShift押しながら線を引くことでできる
  - ペンだけでなく消しゴム、範囲選択モードでも使用可能



## 塗りつぶしのコツ

消したいメッシュ部分のUVマップの線を隠すように塗りつぶすといい感じにできます。
一部分のみ削除もできますが, 断面が綺麗にならないことが多いです。ご了承ください。

## インストール方法
いずれかの手順でインストールできます

### 手順1 unitypackageを利用

1. [GitHub](https://github.com/gatosyocora/MeshDeleterWithTexture/releases/latest)または[BOOTH](https://gatosyocora.booth.pm/items/1501527)で最新のバージョンのUnityPackageをダウンロードする
2. MeshDeleterWithTexture_vx.x.x.unitypackageをUnityにドラッグ&ドロップでインストールする

### 手順2 Unity Package Managerを利用

1. Unity2019以降でのWindow > PackageManagerを開く
2. 左上の+を選択し、`Add package from git URL...`を選択する
3. 以下のURLを入力し、`Add`を選択する

https://github.com/gatosyocora/MeshDeleterWithTexture.git?path=/Assets/MeshDeleterWithTexture

## アンインストール方法

インストールしたUnityプロジェクトのAssetsまたはPackagesフォルダの下にある「MeshDeleterWithTexture」のフォルダごと削除する

## 利用規約

本ツールおよびそのソースコードの著作権はgatosyocoraに帰属します。
本ツールを使ったことによって起こった問題に対して, gatosyocoraは一切の責任を負いかねますのであらかじめご了承ください。
また, 本ツールはMITライセンスで運用されます。詳しくはLICENSE.txtをご覧ください。

## 既知のバグ

- エディタを閉じたり, メッシュを変えたりしてもアバターの塗りつぶしが残っている場合やマテリアルが無くなっている場合は、元のマテリアルに戻してください
  - プレビュー用にマテリアルを変更しているだけなので戻せる
- 使用時に「Assertion failed～」という警告メッセージがUnity左下に表示されますが処理には影響しません。
- 2018.4.20f1(もしかしたらこれ以前でも発生するかもしれない)でBuild PlatformをAndroidにしている場合, ComputeShaderが動かず, 本ツールが動作しない

## 動作保証環境

Unity 2019.4.31f1

## 更新履歴

[CHANGELOG](./CHANGELOG.md)を確認してください

## 連絡先

- Twitter @gatosyocora
- Discord gatosyocora#9575
