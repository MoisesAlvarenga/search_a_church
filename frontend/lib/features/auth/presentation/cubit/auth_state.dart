import 'package:equatable/equatable.dart';
import '../../data/models/user_model.dart';

abstract class AuthState extends Equatable {
  const AuthState();

  @override
  List<Object?> get props => [];
}

class AuthInitial extends AuthState {}

class Unauthenticated extends AuthState {}

class Authenticating extends AuthState {}

class Authenticated extends AuthState {
  final UserModel user;

  const Authenticated(this.user);

  @override
  List<Object?> get props => [user];
}

class AuthError extends AuthState {
  final String message;
  final String? errorCode;
  final int? retryAfterSeconds;

  const AuthError(
    this.message, {
    this.errorCode,
    this.retryAfterSeconds,
  });

  @override
  List<Object?> get props => [message, errorCode, retryAfterSeconds];
}
