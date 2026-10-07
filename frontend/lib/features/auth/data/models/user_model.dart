import 'package:equatable/equatable.dart';

class UserModel extends Equatable {
  final String id;
  final String email;
  final String name;
  final String role;
  final bool isVerifiedRepresentative;

  const UserModel({
    required this.id,
    required this.email,
    required this.name,
    required this.role,
    required this.isVerifiedRepresentative,
  });

  factory UserModel.fromJson(Map<String, dynamic> json) {
    return UserModel(
      id: json['id'] as String,
      email: json['email'] as String,
      name: json['name'] as String,
      role: json['role'] as String,
      isVerifiedRepresentative: json['isVerifiedRepresentative'] as bool? ?? false,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'email': email,
      'name': name,
      'role': role,
      'isVerifiedRepresentative': isVerifiedRepresentative,
    };
  }

  @override
  List<Object?> get props => [id, email, name, role, isVerifiedRepresentative];
}
