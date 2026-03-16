# Changelog

## [1.1.0](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/compare/v1.0.0...v1.1.0) (2026-03-16)


### Features

* add incremental generator detection pipeline and ValueObjectModel ([acaf3e6](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/commit/acaf3e6bc081b6a14e5edf7a4b9e0b52f2fcaddd))
* add ValueObject, EqualityMember, IgnoreEqualityMember attributes ([aa833c2](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/commit/aa833c28205ca38f48eddeba72bf151c4ca5e422))
* implement source emission - Equals, GetHashCode, operators, ToString ([d7fa24b](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/commit/d7fa24b5d0c095e9946cbca20c327dd61f35c169))


### Documentation

* add README with usage, benchmarks, and attribute reference ([6366c25](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/commit/6366c257f78bb73a3639771fa77d63fdd71a25b5))
* rewrite README to lead with record comparison and design rationale ([1d18a1c](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/commit/1d18a1c125b9893afcec33036f2c705867939bef))


### Tests

* add first snapshot test for multi-property class generation ([626635e](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/commit/626635ea5bd69f56737c72a2002e72f616259d93))
* add functional correctness tests for generated ValueObject ([bd6c511](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/commit/bd6c511d2a7f1f5947165b9a48a2056d047c562a))
* add snapshot tests for struct, EqualityMember, nullable scenarios ([ce92ff7](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/commit/ce92ff7277bbbc1b72f98f7980beec6a52783fa2))
* fill coverage gaps - ForceClass, edge cases, struct/attribute functional tests ([1f4808b](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/commit/1f4808bb551b200a88e46933c11ad39d816aec63))
