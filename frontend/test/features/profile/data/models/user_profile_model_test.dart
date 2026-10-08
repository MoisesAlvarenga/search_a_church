import 'package:flutter_test/flutter_test.dart';
import 'package:search_a_church_app/features/profile/data/models/user_profile_model.dart';

void main() {
  group('UserProfileModel', () {
    const rawJson = {
      'userId': 'usr-123',
      'name': 'João da Silva',
      'email': 'joao@email.com',
      'denomination': 'Batista',
      'worshipStyle': 'Contemporâneo',
      'preferredLanguages': ['pt', 'en'],
      'defaultRadiusKm': 15.0,
      'selectedTags': ['rampa_acesso', 'estacionamento_proprio'],
      'isConfigured': true,
    };

    test('fromJson deserializes full payload correctly', () {
      final model = UserProfileModel.fromJson(rawJson);

      expect(model.userId, 'usr-123');
      expect(model.name, 'João da Silva');
      expect(model.email, 'joao@email.com');
      expect(model.denomination, 'Batista');
      expect(model.worshipStyle, 'Contemporâneo');
      expect(model.preferredLanguages, ['pt', 'en']);
      expect(model.defaultRadiusKm, 15.0);
      expect(model.selectedTags, ['rampa_acesso', 'estacionamento_proprio']);
      expect(model.isConfigured, true);
    });

    test('toJson serializes model correctly', () {
      final model = UserProfileModel.fromJson(rawJson);
      final json = model.toJson();

      expect(json['userId'], 'usr-123');
      expect(json['name'], 'João da Silva');
      expect(json['defaultRadiusKm'], 15.0);
      expect(json['selectedTags'], ['rampa_acesso', 'estacionamento_proprio']);
      expect(json['isConfigured'], true);
    });

    test('copyWith updates properties properly', () {
      final model = UserProfileModel.fromJson(rawJson);
      final updated = model.copyWith(
        denomination: 'Presbiteriana',
        defaultRadiusKm: 25.0,
      );

      expect(updated.denomination, 'Presbiteriana');
      expect(updated.defaultRadiusKm, 25.0);
      expect(updated.name, model.name);
    });
  });

  group('UpdateUserProfileRequestModel', () {
    test('toJson produces expected dictionary with optional values', () {
      const request = UpdateUserProfileRequestModel(
        denomination: 'Metodista',
        worshipStyle: 'Tradicional',
        preferredLanguages: ['pt'],
        defaultRadiusKm: 20.0,
        tagCodes: ['rampa_acesso'],
      );

      final json = request.toJson();
      expect(json['denomination'], 'Metodista');
      expect(json['worshipStyle'], 'Tradicional');
      expect(json['defaultRadiusKm'], 20.0);
      expect(json['tagCodes'], ['rampa_acesso']);
      expect(json.containsKey('targetUserId'), false);
    });
  });

  group('DeleteAccountResponseModel', () {
    test('fromJson deserializes confirmation properly', () {
      final json = {
        'success': true,
        'message': 'Conta encerrada e anonimizada sob a LGPD',
      };

      final model = DeleteAccountResponseModel.fromJson(json);
      expect(model.success, true);
      expect(model.message, contains('LGPD'));
    });
  });
}
