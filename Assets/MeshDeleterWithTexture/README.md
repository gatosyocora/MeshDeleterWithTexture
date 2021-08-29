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
   - "DrawType"を"Pen"にするように選択してテクスチャの削除したいところを塗りつぶす
   - "Eraser"を選択することで一部の塗りつぶしを消したり, "Clear All Drawing"ですべての塗りつぶしを削除したりできる
   - "Pen/Eraser size"を変更することでペンの大きさを変えられる
   - 塗りつぶしの際にマウスのホイールを動かしたり, "Scale"を変更することでテクスチャ表示画面の拡大縮小ができる
   - 右クリックをしながらマウスを動かすことで表示箇所を移動できる
6. 削除したい箇所の塗りつぶしが終わったら"Output Mesh"の"SaveFolder"と"Name"でメッシュ書き出しの設定をおこなう
7. "Delete Mesh"を押すと塗りつぶしの場所に対応した箇所を削除したメッシュが書き出され, 自動的に設定される

## その他機能

### Revert

- メッシュ削除後, Ctrl+Zでメッシュ削除前に戻せる
- SkinnedMeshRendererのMeshから別のメッシュに変更できる
- "Revert Mesh to Prefab"でfbxまたはPrefabに設定されているデフォルトのメッシュに戻せる
- "Revert Mesh to previously"で削除前のメッシュに戻せる

### DeleteMask

- "Import DeleteMask"は削除したい箇所をマスク画像で指定できる
  - マスク画像はテクスチャと同じ大きさで黒の部分が塗りつぶされる
  - 現在, png形式とjpg形式に対応
- "Export DeleteMask"で塗りつぶした箇所を黒色, それ以外を白色としたマスク画像をpng形式で出力できる
- "Drag & Drop DeleteMeskTexture"にマスク画像をドラッグアンドドロップしても読み込める
  - 現在はpngとjpgに対応している

### UVMap

- "Export UVMap"でUVマップテクスチャを出力できる
- "UVMap LineColor"でUVマップの線の色を変更できる

### Inverse

- "Inverse FillArea"で塗りつぶされた箇所が反転する
  - 塗られている箇所->塗られていない
  - 塗られていない箇所->塗られている

### Undo

- "Undo Drawing"でPenでの塗りつぶしやInverse FillArea, Clear All Drawingを実行前の状態に戻せる
  - Zキーでもこの機能を使用
  - 最大10回までさかのぼれる



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

Unity 2019.4.29f1

## 更新履歴

[CHANGELOG](./CHANGELOG.md)を確認してください

## 連絡先

- Twitter @gatosyocora
- Discord gatosyocora#9575
