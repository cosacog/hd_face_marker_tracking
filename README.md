# hd_face_marker_tracking
kinect: Face and marker tracking. View marker camera space points

顔とその上からTMSコイル(反射マーカーを固定)のtrackingを想定して作りました。

C#の勉強も兼ねてます。

opencv3 (opencvsharp3経由)使ってます。blob(ピクセルの集団)を同定するところで利用してます。

検出したblobに赤の四角を表示します。同時にblobのx,y,z(単位cm)を表示してます。

サイズでfilterをかけてるので大きすぎると検出されません。

反射マーカー8mm程度のサイズでテストしたところ、しばしば距離情報が取れないようなので、もう少し大きいマーカーを使うことを考えてます。

あるいは3角のちょっと大きなマーカーを使うとか、そんな方向も検討中です。
