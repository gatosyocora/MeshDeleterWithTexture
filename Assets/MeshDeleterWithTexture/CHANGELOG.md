# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.8.0] - 2021-09-25
### Added
- ペンで書いて範囲選択する機能を追加
- 範囲選択した部分を塗りつぶす機能を追加
- 範囲選択した部分を反転する機能を追加
- 直線モードを追加
### Changed
- ペンや消しゴムの線を滑らかになるように変更
- 消しゴム選択時にカーソルが専用のデザインに変わるようにした
- 塗りつぶしを反転する機能を高速化
- 塗りつぶしを反転させたときに塗る色を指定できるように変更
- ドラッグ&ドロップ部分のダークテーマ対応のために実装を変更（まだ直っていない）
- コードの可読性向上のためのいくつかのリファクタリングを実施
- 色選択部分のテキストの文字を変更
### Fixed
- テクスチャを変更したときにカーソルのサイズが変わっていなかった不具合を修正

## [0.7.1] - 2021-09-05
### Fixed
- 拡大時にカーソルがずれる不具合を修正
- Reset時に選択しているTextureがIndex0のものになるのを修正
- 一部ダークテーマ対応漏れを修正

## [0.7.0] - 2021-08-29
### Added
- 言語を切り替える機能を追加(EN, JA)
- 削除後にSubMeshが0個になる場合の暫定対応
### Changed
- 使用するAssetの管理方法を変更
- 塗りで使用していたMaterialを毎回生成するように変更
### Fixed
- UVがミラーリングされているメッシュで塗った部分と異なるポリゴンが消える不具合を修正
- SubMeshを消していないのに選択しているTextureがIndex0のMaterialになっていた不具合を修正

## [0.6.3] - 2021-08-23
### Added
- Unity Package Managerに対応
- CHANGELOG.mdを追加
- アセンブリ定義ファイルを追加
### Fixed
- Mesh切り替え時に前のRendererに対してResetMaterialしていた不具合を修正
- Meshを未選択状態に切り替えてもDrawAreaがリセットされていなかった不具合を修正

## [0.6.2] - 2021-05-05
### Changed
- 削除処理でポリゴン数が0になったSubMeshを削除
- プレビューでマテリアルを差し替えるように
- 削除を繰り返したときにMeshの名前が長くならないように対応

## [0.6.1] - 2020-12-06
### Added
- MeshRendererに対応
- 削除前のMeshに戻す機能を追加
- UVマップの線の色を変更できるように
- BuildTargetがAndroidの場合, 使えないことを示すメッセージを表示
### Changed
- 削除処理をキャンセルできるように
- 削除処理の一部高速化
### Fixed
- 拡大縮小とスクロール時にうまく更新できていなかった不具合を修正

## [0.6.0] - 2020-05-10
### Added
- 塗りつぶし箇所を反転する機能を追加
- 塗りつぶしのUndo機能の追加
- ドラッグ&ドロップでDeleteMaskTextureを読み込めるように
### Changed
- UIの調整

## [0.5.2] - 2020-04-13
### Fixed
- VRChat/Mobile/ToonLit.shaderなど頂点カラーを使うシェーダーで削除後のメッシュが黒くなる不具合を修正

## [0.5.1] - 2019-11-11
### Added
- 同じテクスチャや同じマテリアルが適用されているメッシュにも対応

## [0.5.0] - 2019-09-19
### Added
- ペンカーソルを表示
- UVマップは表示されるように変更
- UVマップテクスチャを出力する機能を追加
### Changed
- テクスチャを塗る機能をGPU処理に変更
- メッシュ削除処理を高速化
### Fixed
- マスク画像のエクスポート時にマテリアルからテクスチャが剥がれる不具合を修正
- ペン選択時に色が黒色にリセットされる不具合を修正

## [0.4.1] - 2019-08-17
### Changed
- 拡大縮小機能の内部処理を変更

## [0.4.0] - 2019-08-14
### Added
- 拡大縮小機能を追加
### Changed
- UIを少し変更
### Fixed
- マスク画像のエクスポート時にマテリアルからテクスチャが剥がれる不具合を修正
- ペン選択時に色が黒色にリセットされる不具合を修正

## [0.3.0] - 2019-08-12
### Changed
- ポリゴン削除の方法を変更

## [0.2.0] - 2019-08-12
### Added
- ペン色を追加（色は見えやすくするためで効果の違いはありません）
- 塗りつぶした箇所がSceneビューのアバターに反映されるように
- jpg形式もマスク画像として読み込めるように

## [0.1.0] - 2019-08-12
### Added
- ツールを公開

[Unreleased]: https://github.com/gatosyocora/MeshDeleterWithTexture/compare/v0.8.0...HEAD
[0.7.1]: https://github.com/gatosyocora/MeshDeleterWithTexture/compare/v0.7.1...v0.8.0
[0.7.1]: https://github.com/gatosyocora/MeshDeleterWithTexture/compare/v0.7.0...v0.7.1
[0.7.0]: https://github.com/gatosyocora/MeshDeleterWithTexture/compare/v0.6.3...v0.7.0
[0.6.3]: https://github.com/gatosyocora/MeshDeleterWithTexture/compare/v0.6.2...v0.6.3
[0.6.2]: https://github.com/gatosyocora/MeshDeleterWithTexture/compare/v0.6.1...v0.6.2
[0.6.1]: https://github.com/gatosyocora/MeshDeleterWithTexture/compare/v0.6.0...v0.6.1
[0.6.0]: https://github.com/gatosyocora/MeshDeleterWithTexture/compare/v0.5.2...v0.6.0
[0.5.2]: https://github.com/gatosyocora/MeshDeleterWithTexture/compare/v0.5.1...v0.5.2
[0.5.1]: https://github.com/gatosyocora/MeshDeleterWithTexture/compare/v0.5.0...v0.5.1
[0.5.0]: https://github.com/gatosyocora/MeshDeleterWithTexture/tree/v0.5.0
