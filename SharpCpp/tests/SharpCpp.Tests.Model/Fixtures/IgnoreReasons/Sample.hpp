#pragma once

// [[sharpcpp::ignore]]
[[sharpcpp::ignore]] int IgnoredBare();
// [[sharpcpp::ignore("manual stub")]]
[[sharpcpp::ignore("manual stub")]] int IgnoredQuoted();

// [[sharpcpp::ignore("prescan only")]]
int PrescanIgnored();
